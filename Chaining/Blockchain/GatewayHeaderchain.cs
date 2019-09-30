﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using BToken.Networking;

namespace BToken.Chaining
{
  public partial class Blockchain
  {
    partial class GatewayHeaderchain : IGateway
    {
      Blockchain Blockchain;
      Network Network;
      
      readonly object LOCK_IsSyncing = new object();
      bool IsSyncing;
      bool IsSyncingCompleted;



      public GatewayHeaderchain(
        Blockchain blockchain,
        Network network, 
        Headerchain headerchain)
      {
        Blockchain = blockchain;
        Network = network;
      }
      


      const int COUNT_HEADER_SESSIONS = 4;
      DataBatch BatchLoadedLast;

      public async Task Synchronize(DataBatch batchInsertedLast)
      {
        BatchLoadedLast = batchInsertedLast;

        Task[] syncHeaderchainTasks = new Task[COUNT_HEADER_SESSIONS];

        for (int i = 0; i < COUNT_HEADER_SESSIONS; i += 1)
        {
          syncHeaderchainTasks[i] = 
            new SyncHeaderchainSession(this).Start();
        }

        await Task.WhenAll(syncHeaderchainTasks);

        await Task.Delay(3000);

        Console.WriteLine("Chain synced to hight {0}",
          Blockchain.Chain.GetHeight());
      }


      IEnumerable<byte[]> LocatorHashes;
      int IndexHeaderBatch;
      DataBatch HeaderBatchOld;
      TaskCompletionSource<object> SignalStartHeaderSyncSession =
        new TaskCompletionSource<object>();

      DataBatch CreateHeaderBatch()
      {
        int batchIndex;
        IEnumerable<byte[]> locatorHashes;

        lock (LOCK_IsSyncing)
        {
          batchIndex = IndexHeaderBatch;

          if (LocatorHashes == null)
          {
            LocatorHashes = Blockchain.GetLocatorHashes();
          }

          locatorHashes = LocatorHashes;
        }

        var headerBatch = new DataBatch(batchIndex);

        headerBatch.ItemBatchContainers.Add(
          new HeaderBatchContainer(
            headerBatch,
            locatorHashes));

        return headerBatch;
      }



      public void ReportInvalidBatch(DataBatch batch)
      {
        Console.WriteLine("Invalid batch {0} reported",
          batch.Index);

        throw new NotImplementedException();
      }
    }
  }
}
