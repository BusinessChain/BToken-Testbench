﻿using System;
using System.Collections.Generic;

using BToken.Networking;

namespace BToken.Chaining
{
  public partial class Blockchain
  {
    public class ChainBlock
    {
      public NetworkHeader Header;

      public ChainBlock BlockPrevious;
      public List<ChainBlock> BlocksNext = new List<ChainBlock>();
      public BlockStore BlockStore;

      public ChainBlock(
        UInt32 version,
        UInt256 hashPrevious,
        UInt256 payloadHash,
        UInt32 unixTimeSeconds,
        UInt32 nBits,
        UInt32 nonce,
        BlockStore blockStore)
      {
        Header = new NetworkHeader(
          version,
          hashPrevious,
          payloadHash,
          unixTimeSeconds,
          nBits,
          nonce);

        BlockStore = blockStore;
      }

      public ChainBlock(NetworkHeader header)
      {
        Header = header;
      }
      public ChainBlock(NetworkHeader header, BlockStore blockStore)
        : this(header)
      {
        BlockStore = blockStore;
      }

    }
  }
}
