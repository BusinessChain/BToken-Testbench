﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace BToken.Networking
{
  partial class Network
  {
    partial class Peer
    {
      Network NetworkAdapter;
      IPEndPoint IPEndPoint;
      PeerConnectionManager ConnectionManager;

      TcpClient TcpClient;
      MessageStreamer MessageStreamer;


      // API
      public Peer(IPEndPoint ipEndPoint, Network networkAdapter)
      {
        NetworkAdapter = networkAdapter;

        ConnectionManager = new PeerConnectionManager(this);
        IPEndPoint = ipEndPoint;
      }


      public async Task startAsync(uint blockheightLocal)
      {
        try
        {
          TcpClient = new TcpClient(new IPEndPoint(IPAddress.Any, 8333));
          await TcpClient.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port);
          MessageStreamer = new MessageStreamer(TcpClient.GetStream());

          await handshakeAsync(blockheightLocal);

          Task readMessagesTask = ReadMessagesAsync();
        }
        catch (Exception ex)
        {
          MessageStreamer.Dispose();
          TcpClient.Close();

          throw new NetworkProtocolException(string.Format("Connection failed with peer '{0}'", IPEndPoint.Address.ToString()), ex);
        }
      }
      async Task handshakeAsync(uint blockchainHeightLocal)
      {
        await MessageStreamer.WriteAsync(new VersionMessage(blockchainHeightLocal));

        while (!ConnectionManager.isHandshakeCompleted())
        {
          NetworkMessage messageRemote = await MessageStreamer.ReadAsync();
          await ConnectionManager.receiveResponseToVersionMessageAsync(messageRemote);
        }
      }
      async Task ReadMessagesAsync()
      {
        while (true)
        {
          NetworkMessage message = await MessageStreamer.ReadAsync();
          byte[] payload = message.Payload;

          switch (message.Command)
          {
            case "ping":
              PingMessage pingMessage = new PingMessage(payload);
              await MessageStreamer.WriteAsync(new PongMessage(pingMessage.Nonce));
              break;
            case "sendheaders":
              await MessageStreamer.WriteAsync(new SendHeadersMessage());
              break;
            default:
              await NetworkAdapter.WriteMessageToListeners(message);
              break;
          }
        }
      }

      public async Task SendMessageAsync(NetworkMessage networkMessage)
      {
        await MessageStreamer.WriteAsync(networkMessage);
      }

      public async Task GetHeadersAsync(IEnumerable<UInt256> headerLocator, BufferBlock<NetworkHeader> networkHeaderBuffer)
      {
        await MessageStreamer.WriteAsync(new GetHeadersMessage(headerLocator));

        NetworkMessage remoteNetworkMessageHeaders = await WaitUntilMessageType("headers");
        HeadersMessage headerMessageRemote = new HeadersMessage(remoteNetworkMessageHeaders.Payload);
              
        foreach (NetworkHeader header in headerMessageRemote.NetworkHeaders)
        {
          await networkHeaderBuffer.SendAsync(header);
        }
        byte[] hashHeaderLast = Hashing.sha256d(headerMessageRemote.NetworkHeaders.Last().getBytes());
        headerLocator = new List<UInt256>() { new UInt256(hashHeaderLast) };

        if (headerMessageRemote.hasMaxHeaderCount())
        {
          await GetHeadersAsync(headerLocator, networkHeaderBuffer);
        }

        networkHeaderBuffer.Post(null);
      }
    
      async Task<NetworkMessage> WaitUntilMessageType(string messageCommand)
      {
        NetworkMessage networkMessage = await MessageStreamer.ReadAsync();
        if (networkMessage.Command == messageCommand)
        {
          return networkMessage;
        }

        return await WaitUntilMessageType(messageCommand);
      }
      
      public uint getChainHeight()
      {
        return ConnectionManager.getChainHeight();
      }
    }
  }
}
