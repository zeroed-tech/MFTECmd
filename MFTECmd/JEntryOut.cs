﻿using System;

namespace MFTECmd
{
    public class JEntryOut
    {
        public string Name { get; set; }
        public ulong EntryNumber { get; set; }
        public int SequenceNumber { get; set; }
        public ulong ParentEntryNumber { get; set; }
        public int ParentSequenceNumber { get; set; }

        public ulong UpdateSequenceNumber { get; set; }

        public DateTimeOffset UpdateTimestamp { get; set; }

        public string UpdateReasons { get; set; }
        public string FileAttributes { get; set; }
        public long OffsetToData { get; set; }
    }
}