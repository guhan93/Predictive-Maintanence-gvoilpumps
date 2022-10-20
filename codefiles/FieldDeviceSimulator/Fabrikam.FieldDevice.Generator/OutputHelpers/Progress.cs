﻿using System.Collections.Generic;
using System.Linq;

namespace Fabrikam.FieldDevice.Generator.OutputHelpers
{
    public struct Progress
    {
        public Statistic[] Statistics { get; }
        public ColoredMessage[] Messages { get; }

        public Progress(Statistic[] statistics, IEnumerable<ColoredMessage> messages)
        {
            Statistics = statistics;
            Messages = messages.ToArray();
        }

        public Progress(Statistic[] statistics, ColoredMessage message)
        {
            Statistics = statistics;
            Messages = new[] { message };
        }
    }
}
