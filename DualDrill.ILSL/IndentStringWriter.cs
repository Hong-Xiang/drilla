namespace DualDrill.ILSL;

public class IndentStringWriter : StringWriter
{
    private readonly string _indentString;

    private int _depth = 0;
    private bool _onNewLine = true;

    public IndentStringWriter(string indent) { _indentString = indent; }

    public override void WriteLine()
    {
        WriteIndentation();
        base.WriteLine();
        _onNewLine = true;
    }

    public override void WriteLine(char c)
    {
        WriteIndentation();
        base.WriteLine(c);
        _onNewLine = true;
    }

    public override void WriteLine(string? s)
    {
        WriteIndentation();
        base.WriteLine(s);
        _onNewLine = true;
    }

    public override void Write(char c)
    {
        WriteIndentation();
        base.Write(c);
    }

    public override void Write(string? s)
    {
        WriteIndentation();
        base.Write(s);
    }

    public void Indent()
    {
        _onNewLine = true;
        _depth++;
    }

    public void Unindent()
    {
        _onNewLine = true;
        _depth--;
    }

    private void WriteIndentation()
    {
        if (_onNewLine)
        {
            for (var i = 0; i < _depth; i++)
            {
                base.Write(_indentString);
            }
            _onNewLine = false;
        }
    }
}
