﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace BToken.Networking
{
  partial class Network
  {
    class NetworkAddressPool
    {
      List<IPAddress> SeedNodeIPAddresses = new List<IPAddress>();
      
      DateTimeOffset TimeOfLastUpdate = DateTimeOffset.UtcNow;
      
      Random RandomGenerator = new Random();
            


      public IPAddress GetNodeAddress()
      {
        if (SeedNodeIPAddresses.Count == 0)
        {
          DownloadIPAddressesFromSeeds();
        }

        int randomIndex = RandomGenerator
          .Next(SeedNodeIPAddresses.Count);

        IPAddress iPAddress = SeedNodeIPAddresses[randomIndex];
        SeedNodeIPAddresses.Remove(iPAddress);

        return iPAddress;
      }


      void DownloadIPAddressesFromSeeds()
      {
        string[] dnsSeeds = File.ReadAllLines(@"..\..\DNSSeeds");

        foreach (string dnsSeed in dnsSeeds)
        {
          if(dnsSeed.Substring(0,2) == "//")
          {
            continue;
          }

          try
          {
            IPHostEntry iPHostEntry = Dns.GetHostEntry(dnsSeed);

            SeedNodeIPAddresses.AddRange(iPHostEntry.AddressList
              .Where(a => a.AddressFamily == AddressFamily.InterNetwork));
          }
          catch(Exception ex)
          {
            Console.WriteLine("DNS seed error {0}: {1}",
              dnsSeed, 
              ex.Message);
          }
        }

        if (SeedNodeIPAddresses.Count == 0)
        {
          throw new NetworkException("No seed addresses downloaded.");
        }
      }
    }
  }
}
