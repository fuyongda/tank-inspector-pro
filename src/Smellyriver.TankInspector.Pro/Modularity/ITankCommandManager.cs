﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Smellyriver.TankInspector.Pro.Modularity
{
    public interface ITankCommandManager
    {
        IEnumerable<ITankCommand> Commands { get; }
        void Register(ITankCommand command);
    }
}
