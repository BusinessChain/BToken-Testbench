﻿using System;

namespace BToken.Chaining
{
  partial class ChainHeader
  {
    static class TargetManager
    {
      const int RETARGETING_BLOCK_INTERVAL = 2016;
      const ulong RETARGETING_TIMESPAN_INTERVAL = 14 * 24 * 60 * 60; // two weeks in seconds

      const string DIFFICULTY_1_TARGET = "00000000FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
      static readonly UInt256 MaxTarget = new UInt256(DIFFICULTY_1_TARGET);
      const double MAX_TARGET = 2.695994666715064E67;


      public static UInt256 getNextTarget(ChainHeader header)
      {
        uint nextHeight = header.getHeight() + 1;

        if ((nextHeight % RETARGETING_BLOCK_INTERVAL) != 0)
        {
          return header.Target;
        }

        ChainHeader headerIntervalStart = header.getHeaderPrevious(RETARGETING_BLOCK_INTERVAL - 1);

        ulong actualTimespan = limit(header.getUnixTimeSeconds() - headerIntervalStart.getUnixTimeSeconds());
        return calculateTarget(header.Target, actualTimespan);
      }
      static ulong limit(ulong actualTimespan)
      {
        if (actualTimespan < RETARGETING_TIMESPAN_INTERVAL / 4)
        {
          return RETARGETING_TIMESPAN_INTERVAL / 4;
        }

        if (actualTimespan > RETARGETING_TIMESPAN_INTERVAL * 4)
        {
          return RETARGETING_TIMESPAN_INTERVAL * 4;
        }

        return actualTimespan;
      }
      static UInt256 calculateTarget(UInt256 oldTarget, ulong actualTimespan)
      {
        UInt256 newTarget = oldTarget.multiplyBy(actualTimespan).divideBy(RETARGETING_TIMESPAN_INTERVAL);

        return UInt256.Max(MaxTarget, newTarget);
      }

      public static double getDifficulty(UInt256 target)
      {
        return MAX_TARGET / (double)target;
      }
    }
  }
}