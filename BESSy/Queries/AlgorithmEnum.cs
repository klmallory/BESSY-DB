using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public enum AlgorithmEnum
    {
        None = 0,
        Plus,
        Minus,
        Mult,
        Mod,
        Divide,
        Percent,
        OpenParen,
        CloseParen,
        Equal,
        NotEqual,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        And,
        Or,
        Not
    }
}
