﻿using System.Diagnostics;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

using BToken.Networking;


namespace BToken.Chaining
{
  public enum BlockCode { ORPHAN, DUPLICATE, INVALID, PREMATURE };


  public partial class Blockchain : IBlockchain
  {
    Chain MainChain;
    List<Chain> SecondaryChains = new List<Chain>();

    BlockValidator Validator;
    BlockLocator Locator;

    BlockchainController Controller;
    HeaderArchiver Archiver = new HeaderArchiver();


    public Blockchain(
      ChainBlock genesisBlock,
      INetwork network,
      List<BlockLocation> checkpoints)
    {
      Controller = new BlockchainController(network, this, Archiver);
      MainChain = new Chain(genesisBlock);

      Validator = new BlockValidator(checkpoints);
      Locator = new BlockLocator(this);
    }

    public void Start()
    {
      Controller.Start();
    }
    void InsertHeader(NetworkHeader header)
    {
      InsertBlock(header, null);
    }
    public void InsertBlock(NetworkHeader header, IPayloadParser payloadParser)
    {
      ChainProbe probe = GetChainProbe(header.HashPrevious);

      ValidateBlock(probe, header, out UInt256 headerHash, payloadParser);
      
      probe.ConnectHeader(header);

      if (probe.IsTip())
      {
        probe.ExtendChain(headerHash);

        if (probe.Chain == MainChain)
        {
          Locator.Update();
          return;
        }
      }
      else
      {
        probe.ForkChain(headerHash);
        SecondaryChains.Add(probe.Chain);
      }

      if (probe.Chain.IsStrongerThan(MainChain))
      {
        ReorganizeChain(probe.Chain);
      }
    }
    void ValidateBlock(ChainProbe probe, NetworkHeader header, out UInt256 headerHash, IPayloadParser payloadParser)
    {
      Validator.ValidateHeader(probe, header, out headerHash);

      if (payloadParser != null)
      {
        payloadParser.ValidatePayload();
      }
    }
    ChainProbe GetChainProbe(UInt256 hash)
    {
      var probe = new ChainProbe(MainChain);

      if (probe.GotoBlock(hash))
      {
        return probe;
      }

      foreach (Chain chain in SecondaryChains)
      {
        probe.Chain = chain;

        if (probe.GotoBlock(hash))
        {
          return probe;
        }
      }

      return null;
    }
    void ReorganizeChain(Chain chain)
    {
      SecondaryChains.Remove(chain);
      SecondaryChains.Add(MainChain);
      MainChain = chain;

      Locator.Reorganize();
    }

  }
}
