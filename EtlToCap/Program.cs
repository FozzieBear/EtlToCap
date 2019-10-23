﻿using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;

namespace EtlToCap
{
    public static class NetworkRoutines
    {

        static void Main(string[] args)
        {
            if (args.Length ==0)
            {
                Console.WriteLine("Usage: EtlToCap <source ETL file> <destination pcap file>");
                return;
            }
            Console.WriteLine($"Converting File {args[0]} to {args[1]}");
            long result = ConvertEtlToPcap(args[0], args[1], 65536);
            Console.WriteLine($"{result} packets converted.");
        }

        public static long ConvertEtlToPcap(string source, string destination, UInt32 maxPacketSize)
        {
            int result = 0;
            using (BinaryWriter writer = new BinaryWriter(File.Open(destination, FileMode.Create)))
            {

                UInt32 magic_number = 0xa1b23c4d; // Swap to nanosecond pcap format to get the most resolution from ETL
                UInt16 version_major = 2;
                UInt16 version_minor = 4;
                Int32 thiszone = 0; // Set capture timezone to UTC
                UInt32 sigfigs = 0;
                UInt32 snaplen = 0xffff; // Set snaplen to 65535
                UInt32 network = 1; // LINKTYPE_ETHERNET

                writer.Write(magic_number);
                writer.Write(version_major);
                writer.Write(version_minor);
                writer.Write(thiszone);
                writer.Write(sigfigs);
                writer.Write(snaplen);
                writer.Write(network);

                using (var reader = new EventLogReader(source, PathType.FilePath))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            if (record.ProviderName == "Microsoft-Windows-NDIS-PacketCapture") // Only process network packets.
                            {
                                result++;
                                DateTime timeCreated = (DateTime)record.TimeCreated;
                                DateTime normTime = timeCreated.ToUniversalTime(); //normalize time to UTC to match pcap header.
                                UInt32 ts_sec = (UInt32)((normTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
                                UInt32 ts_usec = Convert.ToUInt32(normTime.ToString("fffffff")) * 100; //ETL only has 7dp, pad to 9dp.
                                UInt32 incl_len = (UInt32)record.Properties[2].Value;
                                if (incl_len > maxPacketSize)
                                {
                                   Console.WriteLine($"Packet size of {incl_len} exceeded max packet size {maxPacketSize}, packet ignored");
                                }
                                UInt32 orig_len = incl_len;

                                writer.Write(ts_sec);
                                writer.Write(ts_usec);
                                writer.Write(incl_len);
                                writer.Write(orig_len);
                                writer.Write((byte[])record.Properties[3].Value);

                            }
                        }
                    }
                }
                return result;

            }

        }
    }
}
 
       
    

