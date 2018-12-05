﻿using System.Diagnostics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using BToken.Networking;

namespace BToken.Chaining
{

  partial class Headerchain
  {
    partial class HeaderchainController
    {
      INetwork Network;
      Headerchain Headerchain;
      IHeaderArchiver Archiver;


      public HeaderchainController(INetwork network, Headerchain headerchain, IHeaderArchiver archiver)
      {
        Network = network;
        Headerchain = headerchain;
        Archiver = archiver;
      }

      public async Task StartAsync()
      {
        LoadHeadersFromArchive();

        Task inboundSessionRequestListenerTask = StartInboundRequestListenerAsync();

        await DownloadHeaderchainAsync();

        await Headerchain.Blockchain.InitialBlockDownloadAsync();
        
      }
      void LoadHeadersFromArchive()
      {
        try
        {
          using (var archiveReader = Archiver.GetReader())
          {
            NetworkHeader header = archiveReader.GetNextHeader();

            while (header != null)
            {
              Headerchain.InsertHeader(header);

              header = archiveReader.GetNextHeader();
            }
          }
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex.Message);
        }
      }
      async Task DownloadHeaderchainAsync()
      {
        var sessionHeaderDownload = new SessionHeaderDownload(Headerchain, Archiver);
        await Network.ExecuteSessionAsync(sessionHeaderDownload);
      }

      async Task StartInboundRequestListenerAsync()
      {
        while (true)
        {
          using (INetworkChannel channel = await Network.AcceptChannelInboundRequestAsync())
          {
            List<NetworkMessage> inboundMessages = channel.GetInboundRequestMessages();

            foreach (NetworkMessage inboundMessage in inboundMessages)
            {
              switch (inboundMessage.Command)
              {
                case "inv":
                  //await ProcessInventoryMessageAsync(invMessage);
                  break;

                case "getheaders":
                  var getHeadersMessage = new GetHeadersMessage(inboundMessage);
                  ServeGetHeadersRequest(getHeadersMessage, channel);
                  break;

                case "headers":
                  break;

                case "block":
                  break;

                default:
                  break;
              }
            }
          }
        }
      }
      //async Task ProcessInventoryMessageAsync(NetworkMessage networkMessage)
      //{
      //  InvMessage invMessage = new InvMessage(networkMessage);

      //  if (invMessage.GetBlockInventories().Any()) // direkt als property zu kreationszeit anlegen.
      //  {
      //    await Network.NetworkMessageBufferBlockchain.SendAsync(invMessage).ConfigureAwait(false);
      //  }
      //  if (invMessage.GetTXInventories().Any())
      //  {
      //    await Network.NetworkMessageBufferUTXO.SendAsync(invMessage).ConfigureAwait(false);
      //  };
      //}
      void ProcessHeadersMessage(HeadersMessage headersMessage)
      {
        foreach (NetworkHeader header in headersMessage.Headers)
        {
          try
          {
            Headerchain.InsertHeader(header);
          }
          catch (ChainException ex)
          {
            switch (ex.ErrorCode)
            {
              case BlockCode.ORPHAN:
                //await ProcessOrphanSessionAsync(headerHash);
                return;

              case BlockCode.DUPLICATE:
                return;

              default:
                throw ex;
            }
          }
          
          using (var archiveWriter = Archiver.GetWriter())
          {
            archiveWriter.StoreHeader(header);
          }

          Headerchain.Blockchain.DownloadBlock(header);
        }
      }
      void ServeGetHeadersRequest(GetHeadersMessage getHeadersMessage, INetworkChannel channel)
      {
        List<NetworkHeader> headers = new List<NetworkHeader>();
        var probe = new ChainProbe(Headerchain.MainChain);

        SetProbeToMutualRoot(probe, getHeadersMessage.HeaderLocator);

        NetworkHeader header = headerStreamer.ReadNextHeader();
        while (header != null)
        {
          headers.Add(header);
          header = headerStreamer.ReadNextHeader();
        }

        var headersMessage = new HeadersMessage(headers);
        channel.SendMessageAsync(headersMessage);
      }
      static void SetProbeToMutualRoot(ChainProbe probe, List<UInt256> headerLocator)
      {
        foreach(UInt256 hash in headerLocator)
        {
          if (probe.GoTo(hash)) { return; }
        }
      }
      static void SetStreamerPositionToMutualRoot(HeaderStreamer headerStreamer, List<UInt256> headerLocator)
      {
        // Use probe instead of streamer?
        ChainLocation streamLocation = headerStreamer.ReadNextHeaderLocationTowardRoot();
        while(streamLocation != null)
        {
          if(headerLocator.Any(h => h.IsEqual(streamLocation.Hash)))
          {
            return;
          }

          streamLocation = headerStreamer.ReadNextHeaderLocationTowardRoot();
        }
      }


    }
  }
}