﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BToken.Chaining;

namespace BToken.Bitcoin
{
  class BitcoinBlock : ChainBlock
  {
    public BitcoinBlock(
      UInt32 version,
      UInt256 hashPrevious,
      UInt32 unixTimeSeconds,
      UInt32 nBits,
      UInt32 nonce,
      BitcoinBlockPayload payload)
      : base(
          version,
          hashPrevious,
          unixTimeSeconds,
          nBits,
          nonce,
          payload)
    { }
  }

  class BitcoinGenesisBlock : BitcoinBlock
  {
    public BitcoinGenesisBlock()
     : base(
    #region Block 541718
    version: 0x20000000,
    hashPrevious: new UInt256("00000000000000000020b34a45f3f79e98496d681b6d051d9e3a8382fefa06b4"),
    unixTimeSeconds: 1537131866,
    nBits: 388503969,
    nonce: 3309898033,
    payload: new BitcoinBlockPayload(new UInt256("07aeeb57c22f244e6b3446920cddc1082380ef29befefbfd0644877ba28b9596")))
    #endregion

    #region Block 540288
    //version: 0x20000000,
    //hashPrevious: new UInt256("0000000000000000001877e616b546d1ba5cf9e8b8edd9eba480a4fbb9f02bce"),
    //unixTimeSeconds: 1536290079,
    //nBits: 388503969,
    //nonce: 3607916943,
    //payload: new BitcoinBlockPayload(new UInt256("7a76769b0b393c7df65498cf3148ad3b0a24a36aa6cf43fe0788317e75713764")))
    #endregion

    #region Block 538272
    //version: 0x20000000,
    //hashPrevious: new UInt256("0000000000000000001d9d48d93793aaa85b5f6d17c176d4ef905c7e7112b1cf"),
    //unixTimeSeconds: 1535129431,
    //nBits: 388618029,
    //nonce: 2367954839,
    //payload: new BitcoinBlockPayload(new UInt256("3ad0fa0e8c100db5831ebea7cabf6addae2c372e6e1d84f6243555df5bbfa351")))
    #endregion

    #region GenesisBlock
    //version: 0x01,
    //hashPrevious: new UInt256("0000000000000000000000000000000000000000000000000000000000000000"),
    //unixTimeSeconds: 1231006505,
    //nBits: 0x1d00ffff,
    //nonce: 2083236893,
    //payload: new BitcoinBlockPayload(new UInt256("4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b")))
    #endregion
    { }
  }
}
