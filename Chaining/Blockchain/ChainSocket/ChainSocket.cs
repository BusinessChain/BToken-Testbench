﻿using System.Diagnostics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using BToken.Networking;

namespace BToken.Chaining
{
  public partial class Blockchain
  {
    partial class ChainSocket
    {
      Blockchain Blockchain;

      ChainBlock BlockTip;
      public UInt256 BlockTipHash { get; private set; }
      public uint BlockTipHeight { get; private set; }
      double AccumulatedDifficulty;

      public ChainBlock BlockGenesis { get; private set; }
      public ChainBlock BlockUnassignedPayloadDeepest { get; private set; }

      public SocketProbe Probe { get; private set; }
      BlockLocator Locator;

      ChainSocket SocketStronger;
      public ChainSocket SocketWeaker { get; private set; }


      public ChainSocket(
        Blockchain blockchain,
        ChainBlock blockGenesis,
        UInt256 blockGenesisHash)
        : this(
           blockchain,
           blockTip: blockGenesis,
           blockTipHash: blockGenesisHash,
           blockTipHeight: 0,
           blockGenesis: blockGenesis,
           blockUnassignedPayloadDeepest: null,
           accumulatedDifficultyPrevious: 0,
           blockLocator: new BlockLocator(0, blockGenesisHash))
      { }

      ChainSocket(
        Blockchain blockchain,
        ChainBlock blockTip,
        UInt256 blockTipHash,
        uint blockTipHeight,
        ChainBlock blockGenesis,
        ChainBlock blockUnassignedPayloadDeepest,
        double accumulatedDifficultyPrevious,
        BlockLocator blockLocator)
      {
        Blockchain = blockchain;

        BlockTip = blockTip;
        BlockTipHash = blockTipHash;
        BlockTipHeight = blockTipHeight;
        BlockGenesis = blockGenesis;
        BlockUnassignedPayloadDeepest = blockUnassignedPayloadDeepest;
        AccumulatedDifficulty = accumulatedDifficultyPrevious + TargetManager.GetDifficulty(blockTip.Header.NBits);
        Locator = blockLocator;
        
        Probe = new SocketProbe(this);
      }

      public List<ChainBlock> GetBlocksUnassignedPayload(int batchSize)
      {
        if (AllPayloadsAssigned()) { return new List<ChainBlock>(); }

        ChainBlock block = BlockUnassignedPayloadDeepest;

        var locatorBatchBlocksUnassignedPayload = new List<ChainBlock>();
        while (locatorBatchBlocksUnassignedPayload.Count < batchSize)
        {
          if (block.BlockStore == null)
          {
            locatorBatchBlocksUnassignedPayload.Add(block);
          }

          if (block == BlockTip)
          {
            return locatorBatchBlocksUnassignedPayload;
          }

          block = block.BlocksNext[0];

          if(locatorBatchBlocksUnassignedPayload.Count == 0)
          {
            BlockUnassignedPayloadDeepest = block;
          }
        }

        return locatorBatchBlocksUnassignedPayload;
      }
      public bool AllPayloadsAssigned() => BlockUnassignedPayloadDeepest == null;

      public SocketProbe GetProbeAtBlock(UInt256 hash)
      {
        Probe.Reset();

        while (true)
        {
          if (Probe.IsHash(hash))
          {
            return Probe;
          }

          if (Probe.IsGenesis())
          {
            return null;
          }

          Probe.Push();
        }
      }
      
      public void InsertBlock(ChainBlock block, UInt256 headerHash)
      {
        ValidateHeader(block, headerHash);

        ConnectBlock(block, headerHash);
      }
      void ValidateHeader(ChainBlock block, UInt256 headerHash)
      {
        CheckProofOfWorkClaim(block.Header, headerHash);
        CheckTimeStamp(block.Header);

        Probe.ValidateHeader(block.Header, headerHash);
      }
      void CheckProofOfWorkClaim(NetworkHeader header, UInt256 headerHash)
      {
        if (headerHash.IsGreaterThan(UInt256.ParseFromCompact(header.NBits)))
        {
          throw new BlockchainException(BlockCode.INVALID);
        }
      }
      void CheckTimeStamp(NetworkHeader header)
      {
        if (IsTimestampPremature(header.UnixTimeSeconds))
        {
          throw new BlockchainException(BlockCode.PREMATURE);
        }
      }
      static void ConnectChainBlocks(ChainBlock blockPrevious, ChainBlock block)
      {
        block.BlockPrevious = blockPrevious;
        blockPrevious.BlocksNext.Add(block);
      }
      bool IsTimestampPremature(ulong unixTimeSeconds)
      {
        const long MAX_FUTURE_TIME_SECONDS = 2 * 60 * 60;
        return (long)unixTimeSeconds > (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + MAX_FUTURE_TIME_SECONDS);
      }

      void ConnectBlock(ChainBlock block, UInt256 headerHash)
      {
        ConnectChainBlocks(Probe.Block, block);

        Probe.InsertBlock(block, headerHash);
      }

      public void InsertSocketRecursive(ChainSocket socket)
      {
        if(socket.IsStrongerThan(SocketWeaker))
        {
          ConnectAsSocketWeaker(socket);
        }
        else
        {
          SocketWeaker.InsertSocketRecursive(socket);
        }
      }
      public void ConnectAsSocketWeaker(ChainSocket socket)
      {
        if(socket != null)
        {
          socket.SocketWeaker = SocketWeaker;
          socket.SocketStronger = this;
        }
        
        if (SocketWeaker != null)
        {
          SocketWeaker.SocketStronger = socket;
        }

        SocketWeaker = socket;
      }
      
      void Disconnect()
      {
        if (SocketStronger != null)
        {
          SocketStronger.SocketWeaker = SocketWeaker;
        }
        if (SocketWeaker != null)
        {
          SocketWeaker.SocketStronger = SocketStronger;
        }
      }
      
      public bool IsStrongerThan(ChainSocket socket)
      {
        if (socket == null)
        {
          return true;
        }
        return AccumulatedDifficulty > socket.AccumulatedDifficulty;
      }
      
      UInt256 GetHeaderHash(ChainBlock block)
      {
        if(block == BlockTip)
        {
          return BlockTipHash;
        }

        return block.BlocksNext[0].Header.HashPrevious;
      }

      public List<BlockLocation> GetBlockLocations() => Locator.BlockLocations;


    }
  }
}
