﻿// Copyright (c) Terence Parr, Sam Harwell. All Rights Reserved.
// Licensed under the BSD License. See LICENSE.txt in the project root for license information.
#nullable disable

using Antlr4.Runtime.Sharpen;

namespace Antlr4.Runtime.Atn
{
    public sealed class RuleStartState : ATNState
    {
        public RuleStopState stopState;

        public bool isPrecedenceRule;

        public bool leftFactored;

        public override Antlr4.Runtime.Atn.StateType StateType
        {
            get
            {
                return Antlr4.Runtime.Atn.StateType.RuleStart;
            }
        }
    }
}
