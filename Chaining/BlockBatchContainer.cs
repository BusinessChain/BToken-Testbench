﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace BToken.Chaining
{
  partial class UTXOTable
  {
    public class BlockBatchContainer : DataContainer
    {
      const int AVERAGE_INPUTS_PER_TX = 5;
      public List<TXInput> Inputs = new List<TXInput>(COUNT_TXS_IN_BATCH_FILE * AVERAGE_INPUTS_PER_TX);

      public UTXOIndexUInt32 TableUInt32 = new UTXOIndexUInt32();
      public KeyValuePair<byte[], uint>[] UTXOsUInt32;
      public UTXOIndexULong64 TableULong64 = new UTXOIndexULong64();
      public KeyValuePair<byte[], ulong>[] UTXOsULong64;
      public UTXOIndexUInt32Array TableUInt32Array = new UTXOIndexUInt32Array();
      public KeyValuePair<byte[], uint[]>[] UTXOsUInt32Array;

      public Header HeaderPrevious;
      public Header Header;

      public int BlockCount;

      BlockParser BlockParser;

      public BlockBatchContainer(
        Headerchain headerchain,
        int archiveIndex)
        : base(archiveIndex)
      {
        BlockParser = new BlockParser(headerchain);
      }

      public BlockBatchContainer(
        Headerchain headerchain,
        int archiveIndex,
        byte[] blockBytes)
        : base(
            archiveIndex,
            blockBytes)
      {
        BlockParser = new BlockParser(headerchain);
      }


      public BlockBatchContainer(
        Headerchain headerchain,
        Header header)
      {
        BlockParser = new BlockParser(headerchain);
        Header = header;
      }



      public override void TryParse()
      {
        StopwatchParse.Start();

        try
        {
          BlockParser.Parse(this);
        }
        catch (Exception ex)
        {
          IsValid = false;

          Console.WriteLine(
            "Exception {0} loading archive {1}: {2}",
            ex.GetType().Name,
            Index,
            ex.Message);
        }

        StopwatchParse.Stop();
      }



      public void ConvertTablesToArrays()
      {
        UTXOsUInt32 = TableUInt32.Table.ToArray();
        UTXOsULong64 = TableULong64.Table.ToArray();
        UTXOsUInt32Array = TableUInt32Array.Table.ToArray();
      }


      public void AddInput(TXInput input)
      {
        if (
          !TableUInt32.TrySpend(input) &&
          !TableULong64.TrySpend(input) &&
          !TableUInt32Array.TrySpend(input))
        {
          Inputs.Add(input);
        }
      }



      public void AddOutput(
        byte[] tXHash,
        int countTXOutputs)
      {
        int lengthUTXOBits = CountNonOutputBits + countTXOutputs;

        if (LENGTH_BITS_UINT >= lengthUTXOBits)
        {
          TableUInt32.ParseUTXO(
            lengthUTXOBits,
            tXHash);
        }
        else if (LENGTH_BITS_ULONG >= lengthUTXOBits)
        {
          TableULong64.ParseUTXO(
            lengthUTXOBits,
            tXHash);
        }
        else
        {
          TableUInt32Array.ParseUTXO(
            lengthUTXOBits,
            tXHash);
        }
      }
    }
  }
}
