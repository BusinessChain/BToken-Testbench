﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BToken.Networking;

namespace BToken.Accounting
{
  public interface INetwork
  {
    void QueueSession(INetworkSession session);
    Task<NetworkMessage> GetMessageBitcoinAsync();
  }
}
