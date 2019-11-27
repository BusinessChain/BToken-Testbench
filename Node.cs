using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using BToken.Chaining;
using BToken.Networking;

// Test

namespace BToken
{
  partial class Node
  {
    Network Network;
    UTXOTable UTXOTable;
    Headerchain Headerchain;

    Wallet Wallet;

    BitcoinGenesisBlock GenesisBlock = new BitcoinGenesisBlock();
    List<HeaderLocation> Checkpoints = new List<HeaderLocation>()
      {
        new HeaderLocation(height : 11111, hash : "0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"),
        new HeaderLocation(height : 250000, hash : "000000000000003887df1f29024b06fc2200b55f8af8f35453d7be294df2d214"),
        new HeaderLocation(height : 535419, hash : "000000000000000000209ecbacceb3e7b8ec520ed7f1cfafbe149dd2b9007d39")
      };

    byte[] StopHashTestSynchronization =
      "0000000067a97a2a37b8f190a17f0221e9c3f4fa824ddffdc2e205eae834c8d7"
      .ToBinary();

    bool IsDUTSynchronized = false;


    public Node()
    {
      Network = new Network();

      Headerchain = new Headerchain(
        GenesisBlock.Header,
        Checkpoints);
      Headerchain.Network = Network;

      UTXOTable = new UTXOTable(
        GenesisBlock.BlockBytes,
        Headerchain);
      UTXOTable.Network = Network;

      Wallet = new Wallet();
    }

    public async Task StartAsync()
    {
      StartListener();

      Network.Start();

      await Headerchain.Start();

      await UTXOTable.Start();

      StartTestMiner();

      Wallet.GeneratePublicKey();
    }

    async Task StartListener()
    {
      while (true)
      {
        Network.INetworkChannel channel =
          await Network.AcceptChannelInboundRequestAsync();

        List<NetworkMessage> messages = channel.GetApplicationMessages();

        if (!UTXOTable.Synchronizer.GetIsSyncingCompleted())
        {
          channel.Release();
          continue;
        }

        foreach (NetworkMessage message in messages)
        {
          try
          {
            if (channel.IsConnectionTypeInbound())
            {
              Console.WriteLine("{0} message from {1}",
                message.Command,
                channel.GetIdentification());
            }

            switch (message.Command)
            {
              case "getdata":
                var getDataMessage = new GetDataMessage(message);

                foreach (Inventory inventory in getDataMessage.Inventories)
                {
                  if (inventory.Type == InventoryType.MSG_BLOCK)
                  {
                    Console.WriteLine("requesting block {0}",
                      inventory.Hash.ToHexString());

                    if (UTXOTable.Synchronizer.TryGetBlockFromArchive(
                      inventory.Hash,
                      out byte[] blockBytes))
                    {
                      NetworkMessage blockMessage = new NetworkMessage(
                        "block",
                        blockBytes);

                      await channel.SendMessage(blockMessage);
                    }
                    else
                    {
                      // Send reject message;
                    }
                  }
                }

                break;

              case "getheaders":
                var getHeadersMessage = new GetHeadersMessage(message);

                Console.WriteLine("Locator:");
                getHeadersMessage.HeaderLocator.ToList().ForEach(
                  hash => Console.WriteLine(hash.ToHexString()));

                List<Header> headers;

                if (IsDUTSynchronized == false)
                {
                  headers = Headerchain.GetHeaders(
                    getHeadersMessage.HeaderLocator,
                    2000,
                    StopHashTestSynchronization);

                  if (headers.Count == 0)
                  {
                    IsDUTSynchronized = true;
                  }
                }
                else
                {
                  headers = Headerchain.GetHeaders(
                    getHeadersMessage.HeaderLocator,
                    1,
                    getHeadersMessage.StopHash);
                }

                Console.WriteLine("send headers:");
                headers.ForEach(
                  h => Console.WriteLine(h.HeaderHash.ToHexString()));

                await channel.SendMessage(
                  new HeadersMessage(headers));

                break;

              case "inv":
                var invMessage = new InvMessage(message);

                foreach (Inventory inv in invMessage.Inventories
                  .Where(inv => inv.Type == InventoryType.MSG_BLOCK).ToList())
                {
                  Console.WriteLine("inv message {0} from {1}",
                       inv.Hash.ToHexString(),
                       channel.GetIdentification());

                  if (Headerchain.TryReadHeader(
                    inv.Hash,
                    out Header headerAdvertized))
                  {
                    Console.WriteLine(
                      "Advertized block {0} already in chain",
                      inv.Hash.ToHexString());

                    break;
                  }

                  Headerchain.Synchronizer.LoadBatch();
                  await Headerchain.Synchronizer.DownloadHeaders(channel);
                  
                  if (Headerchain.Synchronizer.TryInsertBatch())
                  {
                    if (!await UTXOTable.Synchronizer.TrySynchronize(channel))
                    {
                      Console.WriteLine("Could not synchronize UTXO, with channel {0}",
                        channel.GetIdentification());
                    }
                  }
                  else
                  {
                    Console.WriteLine(
                      "Failed to insert header message from channel {0}",
                      channel.GetIdentification());
                  }
                }

                break;

              case "headers":
                var headersMessage = new HeadersMessage(message);

                if (Headerchain.Synchronizer.TryInsertHeaderBytes(
                  headersMessage.Payload))
                {
                  headersMessage.Headers.ForEach(
                    h => Console.WriteLine("inserted header {0}",
                    h.HeaderHash.ToHexString()));

                  Console.WriteLine("blockheight {0}", Headerchain.GetHeight());

                  if (!await UTXOTable.Synchronizer.TrySynchronize(channel))
                  {
                    Console.WriteLine("Could not synchronize UTXO, with channel {0}",
                      channel.GetIdentification());
                  }
                }
                else
                {
                  Console.WriteLine("Failed to insert header message from channel {0}",
                    channel.GetIdentification());
                }

                break;

              default:
                break;
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine(
              "Serving inbound request {0} of channel {1} ended in exception {2}",
              message.Command,
              channel.GetIdentification(),
              ex.Message);

            Network.DisposeChannel(channel);
          }
        }

        channel.Release();

      }
    }

    async Task StartTestMiner()
    {
      Headerchain.TryReadHeader(StopHashTestSynchronization, out Header header);

      await Task.Delay(20000);

      while (header.HeadersNext.Any())
      {
        header = header.HeadersNext.First();

        var inventory = new Inventory(
          InventoryType.MSG_BLOCK,
          header.HeaderHash);

        Network.SendToInbound(
          new InvMessage(
            new List<Inventory>() { inventory }));

        //Network.SendToInbound(
        //  new HeadersMessage(
        //    new List<Header>() { header }));

        Console.WriteLine("broadcasted {0}",
          header.HeaderHash.ToHexString());

        await Task.Delay(10000);
      }
    }
  }
}