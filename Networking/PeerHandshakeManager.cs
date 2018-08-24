﻿using System;
using System.Threading.Tasks;

namespace BToken.Networking
{
  partial class Network
  {
    partial class Peer
    {
      class PeerHandshakeManager
      {
        Peer Peer;

        bool VerAckReceived;
        bool MeetsCheckpointRequirement;
        VersionMessage VersionMessageRemote;
        
        public PeerHandshakeManager(Peer peer)
        {
          Peer = peer;
        }

        public async Task ProcessResponseToVersionMessageAsync(NetworkMessage messageRemote)
        {
          switch (messageRemote.Command)
          {
            case "verack":
              VerAckReceived = true;
              break;

            case "version":
              VersionMessageRemote = new VersionMessage(messageRemote.Payload);
              NetworkMessage responseToVersionMessageRemote = GetResponseToVersionMessageRemote();
              await Peer.SendMessageAsync(responseToVersionMessageRemote);
              break;

            case "reject":
              RejectMessage rejectMessage = new RejectMessage(messageRemote.Payload);
              throw new NetworkException(string.Format("Peer rejected handshake: '{0}'", rejectMessage.RejectionReason));

            default:
              throw new NetworkException(string.Format("Handshake aborted: Received improper message '{0}' during handshake session.", messageRemote.Command));
          }
        }
        NetworkMessage GetResponseToVersionMessageRemote()
        {
          string rejectionReason = "";

          if (VersionMessageRemote.ProtocolVersion < ProtocolVersion)
          {
            rejectionReason = string.Format("Outdated version '{0}', minimum expected version is '{1}'.", VersionMessageRemote.ProtocolVersion, ProtocolVersion);
          }

          if (!((ServiceFlags)VersionMessageRemote.NetworkServicesLocal).HasFlag(NetworkServicesRemoteRequired))
          {
            rejectionReason = string.Format("Network services '{0}' do not meet requirement '{1}'.", VersionMessageRemote.NetworkServicesLocal, NetworkServicesRemoteRequired);
          }

          if (VersionMessageRemote.UnixTimeSeconds - getUnixTimeSeconds() > 2 * 60 * 60)
          {
            rejectionReason = string.Format("Unix time '{0}' more than 2 hours in the future compared to local time '{1}'.", VersionMessageRemote.NetworkServicesLocal, NetworkServicesRemoteRequired);
          }

          if (VersionMessageRemote.Nonce == Nonce)
          {
            rejectionReason = string.Format("Duplicate Nonce '{0}'.", Nonce);
          }

          if ((RelayOptionFlags)VersionMessageRemote.RelayOption != RelayOptionFlags.SendTxStandard)
          {
            rejectionReason = string.Format("We only support RelayOption = '{0}'.", RelayOptionFlags.SendTxStandard);
          }

          if (rejectionReason != "")
          {
            return new RejectMessage(VersionMessageRemote.Command, RejectMessage.RejectCode.OBSOLETE, rejectionReason);
          }
          else
          {
            return new VerAckMessage();
          }
        }

        public bool isHandshakeCompleted()
        {
          return VerAckReceived && (VersionMessageRemote != null);
        }
      }
    }

  }
}