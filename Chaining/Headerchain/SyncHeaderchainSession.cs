﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using BToken.Networking;

namespace BToken.Chaining
{
  partial class Headerchain
  {
    partial class HeaderchainSynchronizer : DataSynchronizer
    {
      class SyncHeaderchainSession
      {
        HeaderchainSynchronizer Synchronizer;

        public SyncHeaderchainSession(
          HeaderchainSynchronizer synchronizer)
        {
          Synchronizer = synchronizer;
        }



        Network.INetworkChannel Channel;
        const int TIMEOUT_GETHEADERS_MILLISECONDS = 5000;
        DataBatch HeaderBatchOld;
        DataBatch HeaderBatch;
        bool IsSyncing;

        public async Task Start()
        {
          while (true)
          {
            Channel = await Synchronizer.Headerchain.Network.RequestChannel();

            try
            {
            StartRaceSyncHeaderSession:

              if (Synchronizer.GetIsSyncingCompleted())
              {
                Channel.Release();
                return;
              }

              HeaderBatch = Synchronizer.CreateHeaderBatch();

              await DownloadHeaders();

              while (true)
              {
                if (Synchronizer.GetIsSyncingCompleted())
                {
                  Channel.Release();
                  return;
                }

                lock (Synchronizer.LOCK_IsSyncing)
                {
                  if (!Synchronizer.IsSyncing)
                  {
                    if (HeaderBatch.Index != Synchronizer.IndexHeaderBatch)
                    {
                      goto StartRaceSyncHeaderSession;
                    }
                    else
                    {
                      IsSyncing = true;
                      Synchronizer.IsSyncing = true;

                      Synchronizer.SignalStartHeaderSyncSession 
                        = new TaskCompletionSource<object>();

                      break;
                    }
                  }
                }

                await Synchronizer.SignalStartHeaderSyncSession.Task.ConfigureAwait(false);
              }

              HeaderBatchOld = Synchronizer.HeaderBatchOld;

              while (HeaderBatch.CountItems > 0)
              {
                if (HeaderBatchOld != null)
                {
                  await Synchronizer.InputBuffer.SendAsync(HeaderBatchOld);
                }

                HeaderBatchOld = HeaderBatch;

                HeaderBatch = CreateNextHeaderBatch();

                await DownloadHeaders();
              }

              if (HeaderBatchOld != null)
              {
                HeaderBatchOld.IsFinalBatch = true;

                await Synchronizer.InputBuffer.SendAsync(HeaderBatchOld);
              }

              Synchronizer.SetIsSyncingCompleted();

              Synchronizer.SignalStartHeaderSyncSession.SetResult(null);

              Channel.Release();

              return;
            }
            catch (Exception ex)
            {
              //Console.WriteLine("Exception in SyncHeaderchainSession {0} with channel {1}: '{2}'",
              //  GetHashCode(),
              //  Channel == null ? "'null'" : Channel.GetIdentification(),
              //  ex.Message);

              Synchronizer.Headerchain.Network.DisposeChannel(Channel);

              if (Synchronizer.GetIsSyncingCompleted())
              {
                return;
              }

              if (IsSyncing)
              {
                lock (Synchronizer.LOCK_IsSyncing)
                {
                  Synchronizer.IndexHeaderBatch = HeaderBatch.Index;
                  Synchronizer.LocatorHashes = ((HeaderContainer)HeaderBatch.DataContainers.First())
                    .LocatorHashes;

                  Synchronizer.HeaderBatchOld = HeaderBatchOld;

                  Synchronizer.IsSyncing = false;
                }

                Synchronizer.SignalStartHeaderSyncSession.SetResult(null);
              }
            }
          }
        }



        DataBatch CreateNextHeaderBatch()
        {
          DataBatch batch = new DataBatch(HeaderBatch.Index + 1);

          batch.DataContainers.Add(
            new HeaderContainer(
              new List<byte[]> {
                ((HeaderContainer)HeaderBatch.DataContainers[0])
                .HeaderTip.HeaderHash }));

          return batch;
        }


                     
        async Task DownloadHeaders()
        {
          int timeout = TIMEOUT_GETHEADERS_MILLISECONDS;

          CancellationTokenSource cancellation = new CancellationTokenSource(timeout);

          foreach (HeaderContainer headerBatchContainer
            in HeaderBatch.DataContainers)
          {
            headerBatchContainer.Buffer = await Channel.GetHeaders(
              headerBatchContainer.LocatorHashes,
              cancellation.Token);

            headerBatchContainer.TryParse();

            HeaderBatch.CountItems += headerBatchContainer.CountItems;
          }
        }
      }
    }
  }
}