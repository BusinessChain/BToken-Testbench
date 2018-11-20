﻿using System.Diagnostics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using BToken.Chaining;

namespace BToken.Networking
{
  partial class Network
  {
    partial class Peer : IDisposable, INetworkChannel
    {
      Network Network;
      INetworkMessageReceiver NetworkMessageListener;

      IPEndPoint IPEndPoint;

      bool IsSessionExecutingFlag = false;
      BufferBlock<NetworkMessage> SessionMessageBuffer = new BufferBlock<NetworkMessage>();

      CancellationTokenSource CancellationTokenSourceRequestSession = new CancellationTokenSource();
      Task<INetworkSession> RequestSessionTask;
      
      TcpClient TcpClient;
      MessageStreamer NetworkMessageStreamer;
      public NetworkMessage NetworkMessageReceived { get; private set; }
      


      public Peer(Network network)
      {
        Network = network;
        NetworkMessageListener = network;
      }
      public Peer(TcpClient tcpClient, Network network)
      {
        Network = network;

        TcpClient = tcpClient;
        NetworkMessageStreamer = new MessageStreamer(TcpClient.GetStream());

        IPEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
      }

      public async Task StartAsync()
      {
        await ConnectAsync();

        Task processNetworkMessageTask = ProcessNetworkMessageAsync();
        Task sessionListenerTask = StartSessionListenerAsync();
      }

      async Task ConnectAsync()
      {
        int connectionTries = 0;

        while(true)
        {
          try
          {
            IPAddress iPAddress = Network.AddressPool.GetRandomNodeAddress();
            IPEndPoint = new IPEndPoint(iPAddress, Port);
            await ConnectTCPAsync().ConfigureAwait(false);
            await HandshakeAsync().ConfigureAwait(false);

            return;
          }
          catch (Exception ex)
          {
            Debug.WriteLine("Network::ConnectAsync: " + ex.Message
              + "\nConnection tries: '{0}'", ++connectionTries);
          }
        }
      }
      async Task StartSessionListenerAsync()
      {
        while (true)
        {
          INetworkSession session = await Network.NetworkSessionQueue.ReceiveAsync();
          await ExecuteSessionAsync(session);
        }
      }
      async Task ExecuteSessionAsync(INetworkSession session)
      {
        IsSessionExecutingFlag = true;

        while (IsSessionExecutingFlag)
        {
          try
          {
            await session.StartAsync(this).ConfigureAwait(false);

            IsSessionExecutingFlag = false;
          }
          catch (Exception ex)
          {
            Debug.WriteLine("Peer::ExecuteSessionAsync:" + ex.Message);

            Dispose();

            await ConnectAsync().ConfigureAwait(false);
          }
        }
      }

      public void ConnectListener(INetworkMessageReceiver listener)
      {
        NetworkMessageListener = listener;
      }

      public async Task ConnectTCPAsync()
      {
        TcpClient = new TcpClient();
        await TcpClient.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port).ConfigureAwait(false);
        NetworkMessageStreamer = new MessageStreamer(TcpClient.GetStream());
      }
      public async Task HandshakeAsync()
      {
        await NetworkMessageStreamer.WriteAsync(new VersionMessage()).ConfigureAwait(false);
        
        CancellationToken cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        var handshakeManager = new PeerHandshakeManager(this);
        while (!handshakeManager.isHandshakeCompleted())
        {
          NetworkMessage messageRemote = await NetworkMessageStreamer.ReadAsync(cancellationToken).ConfigureAwait(false);
          await handshakeManager.ProcessResponseToVersionMessageAsync(messageRemote).ConfigureAwait(false);
        }
      }
      public async Task ProcessNetworkMessageAsync(CancellationToken cancellationToken = default(CancellationToken))
      {
        while (true)
        {
          try
          {
            NetworkMessageReceived = await NetworkMessageStreamer.ReadAsync(cancellationToken).ConfigureAwait(false);

            switch (NetworkMessageReceived.Command)
            {
              case "version":
                await ProcessVersionMessageAsync(NetworkMessageReceived).ConfigureAwait(false);
                break;
              case "ping":
                await ProcessPingMessageAsync(NetworkMessageReceived).ConfigureAwait(false);
                break;
              case "addr":
                ProcessAddressMessage(NetworkMessageReceived);
                break;
              case "sendheaders":
                await ProcessSendHeadersMessageAsync(NetworkMessageReceived).ConfigureAwait(false);
                break;
              default:
                NetworkMessageListener.ProcessNetworkMessageAsync(this);
                break;
            }
          }
          catch (Exception ex)
          {
            Debug.WriteLine("Peer::ProcessMessagesAsync: " + ex.Message);

            Dispose();

            await ConnectAsync().ConfigureAwait(false);
          }
        }
      }
      async Task ProcessVersionMessageAsync(NetworkMessage networkMessage)
      {
      }
      async Task ProcessPingMessageAsync(NetworkMessage networkMessage)
      {
        PingMessage pingMessage = new PingMessage(networkMessage);
        await NetworkMessageStreamer.WriteAsync(new PongMessage(pingMessage.Nonce)).ConfigureAwait(false);
      }
      void ProcessAddressMessage(NetworkMessage networkMessage)
      {
        AddressMessage addressMessage = new AddressMessage(networkMessage);
      }
      async Task ProcessSendHeadersMessageAsync(NetworkMessage networkMessage) => await NetworkMessageStreamer.WriteAsync(new SendHeadersMessage()).ConfigureAwait(false);
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


      public async Task SendMessageAsync(NetworkMessage networkMessage) => await NetworkMessageStreamer.WriteAsync(networkMessage).ConfigureAwait(false);



      public void Dispose()
      {
        TcpClient.Close();
        SessionMessageBuffer = new BufferBlock<NetworkMessage>();
      }


      public async Task PingAsync() => await NetworkMessageStreamer.WriteAsync(new PingMessage(Nonce));

      public async Task<NetworkBlock> GetBlockAsync(UInt256 hash, CancellationToken cancellationToken)
      {
        var inventory = new Inventory(InventoryType.MSG_BLOCK, hash);
        await NetworkMessageStreamer.WriteAsync(new GetDataMessage(new List<Inventory>() { inventory })).ConfigureAwait(false);

        while (true)
        {
          NetworkMessage networkMessage = await SessionMessageBuffer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
          var blockMessage = networkMessage as BlockMessage;

          if (blockMessage != null)
          {
            return blockMessage.NetworkBlock;
          }
        }
      }
      public async Task<List<NetworkHeader>> GetHeadersAsync(List<UInt256> headerLocator)
      {
        await NetworkMessageStreamer.WriteAsync(new GetHeadersMessage(headerLocator)).ConfigureAwait(false);

        CancellationToken cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;
        HeadersMessage headersMessage = await ReceiveHeadersMessageAsync(cancellationToken).ConfigureAwait(false);
        return headersMessage.Headers;
      }
      async Task<HeadersMessage> ReceiveHeadersMessageAsync(CancellationToken cancellationToken)
      {
        while (true)
        {
          NetworkMessage networkMessage = await SessionMessageBuffer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
          HeadersMessage headersMessage = networkMessage as HeadersMessage;

          if (headersMessage != null)
          {
            return headersMessage;
          }
        }
      }

    }
  }
}
