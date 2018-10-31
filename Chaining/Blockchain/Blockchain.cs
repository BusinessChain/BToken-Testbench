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


  public partial class Blockchain
  {
    IPayloadParser PayloadParser;
    CheckpointManager Checkpoints;

    BlockchainController Controller;
    Chain MainChain;
    //BlockPayloadLocator BlockLocator;

    private readonly object lockBlockInsertion = new object();


    public Blockchain(
      ChainBlock genesisBlock,
      Network network,
      IPayloadParser payloadParser,
      List<BlockLocation> checkpoints)
    {
      PayloadParser = payloadParser;
      Checkpoints = new CheckpointManager(checkpoints);

      Controller = new BlockchainController(network, this);
      MainChain = new Chain(
        blockchain: this,
        genesisBlock: genesisBlock);

      //BlockLocator = new BlockPayloadLocator(this);
    }

    public async Task StartAsync()
    {
      await Controller.StartAsync();
    }

    public List<BlockLocation> GetBlockLocations() => MainChain.GetBlockLocations();
    
    Chain GetChain(UInt256 hash)
    {
      Chain chain = MainChain;

      while (true)
      {
        if(chain == null)
        {
          throw new BlockchainException(BlockCode.ORPHAN);
        }
        
        if (chain.GetAtBlock(hash))
        {
          return chain;
        }

        chain = chain.GetChainWeaker();
      }
    }

    void InsertHeader(NetworkHeader header)
    {
      InsertBlock(new ChainBlock(header));
    }
    void InsertBlock(ChainBlock chainBlock)
    {
      UInt256 headerHash = new UInt256(Hashing.SHA256d(chainBlock.Header.GetBytes()));

      lock (lockBlockInsertion)
      {
        Chain chainAtBlockPrevious = GetChain(chainBlock.Header.HashPrevious);
        ValidateCheckpoint(chainAtBlockPrevious, headerHash);
        chainAtBlockPrevious.InsertBlock(chainBlock, headerHash);
      }
    }
    void ValidateCheckpoint(Chain probe, UInt256 headerHash)
    {
      uint nextBlockHeight = probe.GetHeight() + 1;

      bool chainLongerThanHighestCheckpoint = probe.GetHeightTip() >= Checkpoints.HighestCheckpointHight;
      bool nextHeightBelowHighestCheckpoint = !(nextBlockHeight > Checkpoints.HighestCheckpointHight);

      if (chainLongerThanHighestCheckpoint && nextHeightBelowHighestCheckpoint)
      {
        throw new BlockchainException(BlockCode.INVALID);
      }

      if (!Checkpoints.ValidateBlockLocation(nextBlockHeight, headerHash))
      { 
        throw new BlockchainException(BlockCode.INVALID);
      }
    }
    void InsertBlock(NetworkBlock networkBlock, BlockStore payloadStoreID)
    {
      var chainBlock = new ChainBlock(networkBlock.Header);
      InsertBlock(chainBlock);
      InsertPayload(chainBlock, networkBlock.Payload, payloadStoreID);
    }
    void InsertPayload(ChainBlock chainBlock, byte[] payload, BlockStore payloadStoreID)
    {
      ValidatePayload(chainBlock, payload);
      chainBlock.BlockStore = payloadStoreID;
    }
    void ValidatePayload(ChainBlock chainBlock, byte[] payload)
    {
      UInt256 payloadHash = PayloadParser.GetPayloadHash(payload);
      if (!payloadHash.IsEqual(chainBlock.Header.PayloadHash))
      {
        throw new BlockchainException(BlockCode.INVALID);
      }
    }

    void InsertChain(Chain chain)
    {
      if (chain.IsStrongerThan(MainChain))
      {
        chain.ConnectAsWeakerChain(MainChain);
        MainChain = chain;
      }
      else
      {
        MainChain.InsertChainRecursive(chain);
      }
    }

    uint GetHeight() => MainChain.GetHeightTip();

    static ChainBlock GetBlockPrevious(ChainBlock block, uint depth)
    {
      if (depth == 0 || block.BlockPrevious == null)
      {
        return block;
      }

      return GetBlockPrevious(block.BlockPrevious, --depth);
    }
    
    List<ChainBlock> GetBlocksUnassignedPayload(int batchSize)
    {
      var blocksUnassignedPayload = new List<ChainBlock>();
      Chain chain = MainChain;

      do
      {
        blocksUnassignedPayload.AddRange(chain.GetBlocksUnassignedPayload(batchSize));
        batchSize -= blocksUnassignedPayload.Count;
        chain = chain.GetChainWeaker();
      } while (batchSize > 0 && chain != null);

      return blocksUnassignedPayload;
    }

  }
}
