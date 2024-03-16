namespace Nop;

public class Var
{
    public char type;
    public string value = "";

    public Var(char type, string value)
    {
        this.type = type;
        this.value = value;
    }
}

public class Program
{
    // Program counter
    static int pc = 0;
    // Identifiers (type, value)
    static Dictionary<string, Var> variables = [];

    static string source = """
print "Hello\n"
print "Should be on another line\n"

koffer = "sum: "
lollo = 45423

pimml = koffer + koffer + " hoho" + lollo
print pimml

""";

    public static void Main(string[] args)
    {
        source += '\0'; Prog(); return;

        source = "";

        if (args.Length < 1)
        {
            Console.WriteLine("usage: nop <filename>");
            Environment.Exit(1);
        }

        try
        {
            // Read the entire file content and append a null termination character
            source = File.ReadAllText(args[0]) + '\0';
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"ERROR: Can't find source file '{args[0]}'.");
            Environment.Exit(1);
        }
        catch (Exception ex) // Catch other potential exceptions (e.g., lack of permissions)
        {
            Console.WriteLine($"ERROR: Problem reading the file '{args[0]}': {ex.Message}");
            Environment.Exit(1);
        }

        Prog();
    }

    // returns the current character while skipping over comments
    static char Look()
    {
        if (source[pc] == '#')
        {
            while (source[pc] != '\n' && source[pc] != '\0')
            {
                pc++;
            }
        }
        return source[pc];
    }

    // takes away and returns the current character
    static char Take()
    {
        var c = Look();
        pc++;
        return c;
    }

    // returns whether a certain string could be taken starting at pc
    static bool TryTakeStr(string word)
    {
        var copyPc = pc;
        foreach (char c in word)
        {
            if (Take() != c)
            {
                pc = copyPc;
                return false;
            }
        }
        return true;
    }

    // returns the next non-whitespace character
    static char Next()
    {
        while (Look() == ' ' || Look() == '\t' || Look() == '\n' || Look() == '\r')
        {
            Take();
        }
        return Look();
    }

    // eats white-spaces, returns whether a certain character could be eaten
    static bool TakeNext(char c)
    {
        if (Next() == c)
        {
            Take();
            return true;
        }
        return false;
    }

    static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    static bool IsAlpha(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    static bool IsAlnum(char c)
    {
        return IsDigit(c) || IsAlpha(c);
    }

    static bool IsAddOp(char c)
    {
        return c == '+' || c == '-';
    }

    static bool IsMulOp(char c)
    {
        return c == '*' || c == '/';
    }

    static string TakeNextAlnum()
    {
        var alnum = "";
        if (IsAlpha(Next()))
        {
            while (IsAlnum(Look()))
            {
                alnum += Take();
            }
        }
        return alnum;
    }

    static int TakeNextNum()
    {
        int num = 0;
        while (IsDigit(Look()))
        {
            num = 10 * num + Take() - '0';
        }

        return num;
    }

    // --------------------------------------------------------------------------------------------------

    static bool BoolFactor(ref bool active)
    {
        var inverse = TakeNext('!');
        var expr = Expr(ref active);
        var result = expr.value;
        var boolResult = false;
        Next();

        if (expr.type == 'i')
        {
            var intNum = int.Parse(result);
            if (TryTakeStr("=="))
            {
                boolResult = intNum == MathExpr(ref active);
            }
            else if (TryTakeStr("!="))
            {
                boolResult = intNum != MathExpr(ref active);
            }
            else if (TryTakeStr("<="))
            {
                boolResult = intNum <= MathExpr(ref active);
            }
            else if (TryTakeStr("<"))
            {
                boolResult = intNum < MathExpr(ref active);
            }
            else if (TryTakeStr(">="))
            {
                boolResult = intNum >= MathExpr(ref active);
            }
            else if (TryTakeStr(">"))
            {
                boolResult = intNum > MathExpr(ref active);
            }
        }
        else
        {
            if (TryTakeStr("=="))
            {
                boolResult = result == StrExpr(ref active);
            }
            else if (TryTakeStr("!="))
            {
                boolResult = result != StrExpr(ref active);
            }
            else
            {
                boolResult = result != "";
            }
        }
        return active && (boolResult != inverse);
    }

    static bool BoolTerm(ref bool active)
    {
        var boool = BoolFactor(ref active);
        while (TakeNext('&'))
        {
            // logical 'and' corresponds to multiplication
            boool = boool & BoolFactor(ref active);
        }
        return boool;
    }

    static bool BoolExpr(ref bool active)
    {
        var boool = BoolTerm(ref active);
        while (TakeNext('|'))
        {
            boool = boool | BoolTerm(ref active);
        }
        return boool;
    }

    static int MathFactor(ref bool active)
    {
        var num = 0;
        if (TakeNext('('))
        {
            num = MathExpr(ref active);
            if (!TakeNext(')'))
            {
                Error("missing ')'");
            }
        }
        else if (IsDigit(Next()))
        {
            num = TakeNextNum();
        }
        else if (TryTakeStr("val("))
        {
            var str = Str(ref active);
            if (active)
            {
                var success = int.TryParse(str, out var intNum);
                if (success)
                {
                    num = intNum;
                }
                else
                {
                    Error("invalid integer value");
                }
            }
            if (!TakeNext(')'))
            {
                Error("missing ')'");
            }
        }
        else
        {
            var ident = TakeNextAlnum();
            variables.TryGetValue(ident, out var val);
            if (!variables.ContainsKey(ident) || val?.type != 'i')
            {
                Error("unknown variable");
            }
            else if (active)
            {
                var success = int.TryParse(val?.value, out var intNum);
                if (success)
                {
                    num = intNum;
                }
                else
                {
                    Error("invalid integer value");
                }
            }
        }
        return num;
    }

    static int MathTerm(ref bool active)
    {
        var num = MathFactor(ref active);
        while (IsMulOp(Next()))
        {
            var c = Take();
            var num2 = MathFactor(ref active);

            if (c == '*') // multiplikation
            {
                num = num * num2;
            }
            else // division
            {
                num = num / num2;
            }
        }

        return num;
    }

    static int MathExpr(ref bool active)
    {
        var c = Next();
        if (IsAddOp(c))
        {
            c = Take();
        }

        var num = MathTerm(ref active);

        if (c == '-')
        {
            num = -num;
        }

        while (IsAddOp(Next()))
        {
            c = Take();
            var num2 = MathTerm(ref active);
            if (c == '+') // addition
            {
                num = num + num2;
            }
            else // subtraction
            {
                num = num - num2;
            }
        }
        return num;
    }

    static string Str(ref bool active)
    {
        var str = "";
        if (TakeNext('\"'))
        {
            while (!TryTakeStr("\""))
            {
                if (Look() == '\0')
                {
                    Error("unexpected EOF");
                }
                if (TryTakeStr("\\n"))
                {
                    str += '\n';
                }
                else
                {
                    str += Take();
                }
            }
        }
        else if (TryTakeStr("str(")) // str(...)
        {
            str = MathExpr(ref active).ToString();
            if (!TakeNext(')'))
            {
                Error("missing");
            }
        }
        else if (TryTakeStr("input()"))
        {
            if (active)
            {
                str = Console.ReadLine();
            }
        }
        else
        {
            var ident = TakeNextAlnum();
            variables.TryGetValue(ident, out var val);
            if (variables.ContainsKey(ident) && val?.type == 's')
            {
                str = val.value;
            }
            else if (val?.type == 'i')
            {
                str = val.value.ToString();
            }
            else if (IsDigit(Look()))
            {
                str = TakeNextNum().ToString();
            }
            else
            {
                Error("expected variable or literal");
            }
        }

        return str ?? "";
    }

    static string StrExpr(ref bool active)
    {
        var str = Str(ref active);
        while (TakeNext('+')) //string addition = concatenation
        {
            str += Str(ref active);
        }
        return str;
    }

    static Var Expr(ref bool active)
    {
        var copyPc = pc;
        var ident = TakeNextAlnum(); // scan for identifier or "str"
        variables.TryGetValue(ident, out var val);
        pc = copyPc;

        if (Next() == '\"' || ident == "str" || ident == "input" || (variables.ContainsKey(ident) && val?.type == 's'))
        {
            return new Var('s', StrExpr(ref active));
        }
        else
        {
            return new Var('i', MathExpr(ref active).ToString());
        }
    }

    static void DoWhile(ref bool active)
    {
        var local = active ? true : false;
        var pcWhile = pc; // save PC of the while statement
        while (BoolExpr(ref local))
        {
            Block(ref local);
            pc = pcWhile;
        }

        // scan over inactive block and leave while
        var fals = false;
        Block(ref fals);
    }

    static void DoIfElse(ref bool active)
    {
        var b = BoolExpr(ref active);
        if (active && b) // process if block?
        {
            Block(ref active);
        }
        else
        {
            var fals = false;
            Block(ref fals);
        }

        Next();
        if (TryTakeStr("else")) // process eles block?
        {
            if (active && !b)
            {
                Block(ref active);
            }
            else
            {
                var fals = false;
                Block(ref fals);
            }
        }
    }

    static void DoGoSub(ref bool active)
    {
        var ident = TakeNextAlnum();
        variables.TryGetValue(ident, out var val);
        if (!variables.ContainsKey(ident) || val?.type != 'p')
        {
            Error("unknown subroutine");
        }
        var ret = pc;
        pc = int.Parse(variables[ident].value);
        Block(ref active);
        pc = ret; // execute block as a subroutine
    }

    static void DoSubDecl()
    {
        var ident = TakeNextAlnum();
        if (ident == "")
        {
            Error("missing subroutine identifier");
        }
        variables[ident] = new Var('p', pc.ToString());
        var fals = false;
        Block(ref fals);
    }

    static void DoAssign(ref bool active)
    {
        // decide what sort of expression follows
        var ident = TakeNextAlnum();
        variables.TryGetValue(ident, out var val);
        if (!TakeNext('=') || ident == "")
        {
            Error("unknown statement");
        }

        var expr = Expr(ref active);
        // assert initialization even if block is inactive
        if (active || !variables.ContainsKey(ident))
        {
            variables[ident] = expr;
        }
    }

    static void DoBreak(ref bool active)
    {
        if (active)
        {
            active = false;
        }
    }

    static void DoPrint(ref bool active)
    {
        // process comma-separated arguments
        while (true)
        {
            var expr = Expr(ref active);
            if (active)
            {
                var val = expr.value;
                Console.Write(val);
            }
            if (!TakeNext(','))
            {
                return;
            }
        }
    }

    static void Stmt(ref bool active)
    {
        if (TryTakeStr("print"))
        {
            DoPrint(ref active);
        }
        else if (TryTakeStr("if"))
        {
            DoIfElse(ref active);
        }
        else if (TryTakeStr("while"))
        {
            DoWhile(ref active);
        }
        else if (TryTakeStr("break"))
        {
            DoBreak(ref active);
        }
        else if (TryTakeStr("gosub"))
        {
            DoGoSub(ref active);
        }
        else if (TryTakeStr("sub"))
        {
            DoSubDecl();
        }
        else
        {
            DoAssign(ref active);
        }
    }

    static void Block(ref bool active)
    {
        if (TakeNext('{'))
        {
            while (!TakeNext('}'))
            {
                Block(ref active);
            }
        }
        else
        {
            Stmt(ref active);
        }
    }

    static void Prog()
    {
        var active = true;
        while (Next() != '\0')
        {
            Block(ref active);
        }
    }
    static void Error(string text)
    {
        int s = source.LastIndexOf('\n', pc - 1) + 1;
        int e = source.IndexOf('\n', pc);
        if (e == -1) e = source.Length;

        int lineNum = source[..pc].Count(c => c == '\n') + 1;
        string errorFragmentBeforePc = source[s..pc];
        string errorFragmentAfterPc = source[pc..e];

        Console.WriteLine($"\nERROR {text} in line {lineNum}: '{errorFragmentBeforePc}_{errorFragmentAfterPc}'\n");

        Environment.Exit(1);
    }
}