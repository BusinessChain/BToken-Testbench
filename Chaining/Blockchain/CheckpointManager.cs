﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BToken.Chaining
{
  partial class Blockchain
  {
    class CheckpointManager
    {
      List<BlockLocation> Checkpoints;


      public CheckpointManager(List<BlockLocation> checkpoints)
      {
        Checkpoints = checkpoints; // sort checkpoints then delete comment in Bitcoin.cs
      }

      public bool ValidateBlockLocation(uint height, UInt256 hash)
      {
        BlockLocation checkpoint = Checkpoints.Find(c => c.Height == height);
        if (checkpoint != null)
        {
          return checkpoint.Hash.isEqual(hash);
        }

        return true;
      }
    }
  }
}
