﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BToken.Chaining;

namespace BToken
{
  partial class Bitcoin
  {
    class BitcoinGenesisChainBlock : Blockchain.ChainBlock
    {
      public BitcoinGenesisChainBlock()
       : base(
           hash: new UInt256("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"),
           hashPrevious: new UInt256("0000000000000000000000000000000000000000000000000000000000000000"),
           nBits: 0x1d00ffff,
           merkleRootHash: new UInt256("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"),
           unixTimeSeconds: 1231006505)
      { }
    }
  }
}
