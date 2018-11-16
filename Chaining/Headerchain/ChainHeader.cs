﻿using System;
using System.Collections.Generic;

using BToken.Networking;

namespace BToken.Chaining
{
  public partial class Headerchain
  {
    class ChainHeader
    {
      public NetworkHeader Header;

      public ChainHeader HeaderPrevious;
      public List<ChainHeader> HeadersNext = new List<ChainHeader>();
      
      public ChainHeader(
        UInt32 version,
        UInt256 hashPrevious,
        UInt256 payloadHash,
        UInt32 unixTimeSeconds,
        UInt32 nBits,
        UInt32 nonce)
      {
        Header = new NetworkHeader(
          version,
          hashPrevious,
          payloadHash,
          unixTimeSeconds,
          nBits,
          nonce);
      }
      
      public ChainHeader(NetworkHeader header, ChainHeader headerPrevious)
      {
        Header = header;
        HeaderPrevious = headerPrevious;
      }

    }
  }
}
