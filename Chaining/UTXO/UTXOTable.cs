﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Linq;

using BToken.Networking;



namespace BToken.Chaining
{
  partial class UTXOTable
  {
    Headerchain Headerchain;

    byte[] GenesisBlockBytes;

    const int COUNT_TXS_IN_BATCH_FILE = 50000;
    const int HASH_BYTE_SIZE = 32;

    const int COUNT_BATCHINDEX_BITS = 16;
    const int COUNT_COLLISION_BITS_PER_TABLE = 2;
    const int COUNT_COLLISIONS_MAX = 2 ^ COUNT_COLLISION_BITS_PER_TABLE - 1;

    const int LENGTH_BITS_UINT = 32;
    const int LENGTH_BITS_ULONG = 64;

    public static readonly int CountNonOutputBits =
      COUNT_BATCHINDEX_BITS +
      COUNT_COLLISION_BITS_PER_TABLE * 3;

    UTXOIndexCompressed[] Tables;
    UTXOIndexUInt32Compressed TableUInt32 = new UTXOIndexUInt32Compressed();
    UTXOIndexULong64Compressed TableULong64 = new UTXOIndexULong64Compressed();
    UTXOIndexUInt32ArrayCompressed TableUInt32Array = new UTXOIndexUInt32ArrayCompressed();

    const int UTXOSTATE_ARCHIVING_INTERVAL = 500;
    static string PathUTXOState = "UTXOArchive";
    static string PathUTXOStateOld = PathUTXOState + "_Old";

    public int BlockHeight;
    int ArchiveIndex;
    Header Header;

    long UTCTimeStartMerger;
    Stopwatch StopwatchMerging = new Stopwatch();

    GatewayUTXO Gateway;

    string ArchivePath = "J:\\BlockArchivePartitioned";


    public UTXOTable(
      byte[] genesisBlockBytes,
      Headerchain headerchain,
      Network network)
    {
      Headerchain = headerchain;

      Tables = new UTXOIndexCompressed[]{
          TableUInt32,
          TableULong64,
          TableUInt32Array };

      GenesisBlockBytes = genesisBlockBytes;

      Gateway = new GatewayUTXO(
        network,
        this);

      Directory.CreateDirectory(ArchivePath);
    }



    public async Task Start()
    {
      await Gateway.Start();
    }


    void InsertUTXO(
      byte[] uTXOKey,
      UTXOIndexCompressed table)
    {
      int primaryKey = BitConverter.ToInt32(uTXOKey, 0);

      for (int c = 0; c < Tables.Length; c += 1)
      {
        if (Tables[c].PrimaryTableContainsKey(primaryKey))
        {
          Tables[c].IncrementCollisionBits(
            primaryKey, 
            table.Address);

          table.AddUTXOAsCollision(uTXOKey);

          return;
        }
      }

      table.AddUTXOAsPrimary(primaryKey);
    }

    void InsertUTXOsUInt32(
      KeyValuePair<byte[], uint>[] uTXOsUInt32)
    {
      int i = 0;

      while (i < uTXOsUInt32.Length)
      {
        TableUInt32.UTXO =
          uTXOsUInt32[i].Value |
          ((uint)ArchiveIndex & UTXOIndexUInt32.MaskBatchIndex);

        InsertUTXO(
          uTXOsUInt32[i].Key,
          TableUInt32);

        i += 1;
      }
    }
    
    void InsertUTXOsULong64(
      KeyValuePair<byte[], ulong>[] uTXOsULong64)
    {
      int i = 0;

      while (i < uTXOsULong64.Length)
      {
        TableULong64.UTXO =
          uTXOsULong64[i].Value |
          ((ulong)ArchiveIndex & UTXOIndexULong64.MaskBatchIndex);

        InsertUTXO(
          uTXOsULong64[i].Key,
          TableULong64);

        i += 1;
      }
    }
    
    void InsertUTXOsUInt32Array(
      KeyValuePair<byte[], uint[]>[] uTXOsUInt32Array)
    {
      int i = 0;

      while (i < uTXOsUInt32Array.Length)
      {
        TableUInt32Array.UTXO = uTXOsUInt32Array[i].Value;
        TableUInt32Array.UTXO[0] |= 
          (uint)ArchiveIndex & UTXOIndexUInt32Array.MaskBatchIndex;

        InsertUTXO(
          uTXOsUInt32Array[i].Key,
          TableUInt32Array);

        i += 1;
      }
    }
    
    void SpendUTXOs(List<TXInput> inputs)
    {
      int i = 0;
    LoopSpendUTXOs:
      while (i < inputs.Count)
      {
        for (int c = 0; c < Tables.Length; c += 1)
        {
          UTXOIndexCompressed tablePrimary = Tables[c];

          if (tablePrimary.TryGetValueInPrimaryTable(inputs[i].PrimaryKeyTXIDOutput))
          {
            UTXOIndexCompressed tableCollision = null;
            for (int cc = 0; cc < Tables.Length; cc += 1)
            {
              if (tablePrimary.HasCollision(cc))
              {
                tableCollision = Tables[cc];

                if (tableCollision.TrySpendCollision(inputs[i], tablePrimary))
                {
                  i += 1;
                  goto LoopSpendUTXOs;
                }
              }
            }

            tablePrimary.SpendPrimaryUTXO(inputs[i], out bool allOutputsSpent);

            if (allOutputsSpent)
            {
              tablePrimary.RemovePrimary();

              if (tableCollision != null)
              {
                tableCollision.ResolveCollision(tablePrimary);
              }
            }

            i += 1;
            goto LoopSpendUTXOs;
          }
        }

        throw new UTXOException(string.Format(
          "Referenced TX {0} not found in UTXO table.",
          inputs[i].TXIDOutput.ToHexString()));
      }
    }


       
    void LoadImage()
    {
      if (TryLoadUTXOState())
      {
        Console.WriteLine("Load UTXO Image from {0}, ArchiveIndex {1}", 
          PathUTXOState,
          ArchiveIndex);
        return;
      }

      if (Directory.Exists(PathUTXOState))
      {
        Directory.Delete(PathUTXOState, true);
      }

      if (Directory.Exists(PathUTXOStateOld))
      {
        Directory.Move(PathUTXOStateOld, PathUTXOState);

        if (TryLoadUTXOState())
        {
          Console.WriteLine("Load UTXO Image from {0}, ArchiveIndex {1}",
            PathUTXOStateOld,
            ArchiveIndex);
          return;
        }

        Directory.Delete(PathUTXOState, true);
      }

      Console.WriteLine("Failed to load UTXO Image from either {0} or {1}" +
        "\n build from genesis, ArchiveIndex {2}",
        PathUTXOState,
        PathUTXOStateOld,
        ArchiveIndex);

      BlockBatchContainer genesisBlockContainer = new BlockBatchContainer(
        new BlockParser(Headerchain),
        0,
        GenesisBlockBytes);

      genesisBlockContainer.Parse();
      
      InsertContainer(genesisBlockContainer);
    }



    bool TryLoadUTXOState()
    {
      try
      {
        byte[] uTXOState = File.ReadAllBytes(Path.Combine(PathUTXOState, "UTXOState"));

        ArchiveIndex = BitConverter.ToInt32(uTXOState, 0);
        BlockHeight = BitConverter.ToInt32(uTXOState, 4);

        byte[] headerHashMergedLast = new byte[HASH_BYTE_SIZE];
        Array.Copy(uTXOState, 8, headerHashMergedLast, 0, HASH_BYTE_SIZE);
        Header = Headerchain.ReadHeader(headerHashMergedLast);

        for (int c = 0; c < Tables.Length; c += 1)
        {
          Tables[c].Load();
        }

        return true;
      }
      catch
      {
        ArchiveIndex = 0;
        BlockHeight = -1;
        Header = null;

        for (int c = 0; c < Tables.Length; c += 1)
        {
          Tables[c].Clear();
        }

        return false;
      }
    }


    void InsertContainer(BlockBatchContainer container)
    {
      StopwatchMerging.Restart();

      InsertUTXOsUInt32(container.UTXOsUInt32);
      InsertUTXOsULong64(container.UTXOsULong64);
      InsertUTXOsUInt32Array(container.UTXOsUInt32Array);

      SpendUTXOs(container.Inputs);

      StopwatchMerging.Stop();

      BlockHeight += container.BlockCount;
    }

    

    const int SIZE_OUTPUT_BATCH = 50000;
    int CountItems;

    List<DataBatchContainer> Containers = new List<DataBatchContainer>();

    bool TryInsertBatch(DataBatch batch)
    {
      try
      {
        foreach (BlockBatchContainer container
          in batch.ItemBatchContainers)
        {
          InsertContainer(container);

          Containers.Add(container);
          CountItems += container.CountItems;

          bool isFinalContainer = batch.IsFinalBatch && 
            (container == batch.ItemBatchContainers.Last());

          if (CountItems > SIZE_OUTPUT_BATCH || isFinalContainer)
          {
            ArchiveContainers(Containers);

            Containers = new List<DataBatchContainer>();
            CountItems = 0;

            ArchiveIndex += 1;

            ArchiveState();
          }
        }
        
        LogInsertion(
          batch.ItemBatchContainers.Sum(
            c => c.StopwatchParse.ElapsedTicks),
          batch.Index);

        return true;
      }
      catch (ChainException ex)
      {
        Console.WriteLine(
          "Insertion of data batch {0} raised ChainException:\n {1}.",
          batch.Index,
          ex.Message);

        return false;
      }
    }


    void ArchiveState()
    {
      if (ArchiveIndex % UTXOSTATE_ARCHIVING_INTERVAL != 0)
      {
        return;
      }

      if (Directory.Exists(PathUTXOState))
      {
        if (Directory.Exists(PathUTXOStateOld))
        {
          Directory.Delete(PathUTXOStateOld, true);
        }
        Directory.Move(PathUTXOState, PathUTXOStateOld);
      }

      Directory.CreateDirectory(PathUTXOState);

      byte[] uTXOState = new byte[40];
      BitConverter.GetBytes(ArchiveIndex).CopyTo(uTXOState, 0);
      BitConverter.GetBytes(BlockHeight).CopyTo(uTXOState, 4);
      Header.HeaderHash.CopyTo(uTXOState, 8);

      using (FileStream stream = new FileStream(
         Path.Combine(PathUTXOState, "UTXOState"),
         FileMode.Create,
         FileAccess.ReadWrite,
         FileShare.Read))
      {
        stream.Write(uTXOState, 0, uTXOState.Length);
      }

      Parallel.ForEach(Tables, t =>
      {
        t.BackupToDisk(PathUTXOState);
      });
    }

    async Task ArchiveContainers(List<DataBatchContainer> containers)
    {
      string filePath =
        Path.Combine(ArchivePath, "p" + ArchiveIndex);

      try
      {
        using (FileStream file = new FileStream(
          filePath,
          FileMode.Create,
          FileAccess.Write,
          FileShare.None,
          bufferSize: 1048576,
          useAsync: true))
        {
          foreach (BlockBatchContainer container in containers)
          {
            await file.WriteAsync(
              container.Buffer,
              0,
              container.Buffer.Length).ConfigureAwait(false);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }



    BlockBatchContainer LoadDataContainer(int containerIndex)
    {
      return new BlockBatchContainer(
        new BlockParser(Headerchain),
        containerIndex,
        File.ReadAllBytes(
          Path.Combine(ArchivePath, "p" + containerIndex)));
    }



    readonly object LOCK_HeaderLoad = new object();
    int IndexLoad;

    public bool TryLoadBatch(
      out DataBatch uTXOBatch,
      int countHeaders)
    {
      lock (LOCK_HeaderLoad)
      {
        if (Header.HeadersNext.Count == 0)
        {
          uTXOBatch = null;
          return false;
        }

        uTXOBatch = new DataBatch(IndexLoad++);

        for (int i = 0; i < countHeaders; i += 1)
        {
          Header = Header.HeadersNext[0];

          BlockBatchContainer blockContainer =
            new BlockBatchContainer(
              new BlockParser(Headerchain),
              Header);

          uTXOBatch.ItemBatchContainers.Add(blockContainer);

          if (Header.HeadersNext.Count == 0)
          {
            uTXOBatch.IsFinalBatch = true;
            break;
          }
        }

        return true;
      }
    }


    void LogInsertion(long elapsedTicksParsing, int index)
    {
      if (UTCTimeStartMerger == 0)
      {
        UTCTimeStartMerger = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      }
      
      int ratioMergeToParse =
        (int)((float)StopwatchMerging.ElapsedTicks * 100
        / elapsedTicksParsing);

      string logCSV = string.Format(
        "{0},{1},{2},{3},{4},{5},{6}",
        index,
        BlockHeight,
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() - UTCTimeStartMerger,
        ratioMergeToParse,
        Tables[0].GetMetricsCSV(),
        Tables[1].GetMetricsCSV(),
        Tables[2].GetMetricsCSV());

      Console.WriteLine(logCSV);
    }
  }
}
