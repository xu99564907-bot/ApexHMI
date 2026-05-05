using System;
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models.RuntimeUi;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>
/// 可撤销/重做的编辑操作。
/// </summary>
public interface IRevertibleEdit
{
    void Undo();
    void Redo();
    string Description { get; }
}

// ========== 具体编辑实现 ==========

/// <summary>移动控件编辑。</summary>
public sealed class MoveWidgetEdit : IRevertibleEdit
{
    private readonly IWidgetEditorService _editor;
    private readonly WidgetInstance _widget;
    private readonly double _oldX, _oldY, _newX, _newY;

    public string Description => $"移动控件 ({_oldX:F0},{_oldY:F0}) → ({_newX:F0},{_newY:F0})";

    public MoveWidgetEdit(IWidgetEditorService editor, WidgetInstance widget,
        double oldX, double oldY, double newX, double newY)
    {
        _editor = editor;
        _widget = widget;
        _oldX = oldX; _oldY = oldY;
        _newX = newX; _newY = newY;
    }

    public void Undo() => _editor.MoveWidget(_widget, _oldX, _oldY);
    public void Redo() => _editor.MoveWidget(_widget, _newX, _newY);
}

/// <summary>控件属性编辑。</summary>
public sealed class PropertyEdit : IRevertibleEdit
{
    private readonly IWidgetEditorService _editor;
    private readonly WidgetInstance _widget;
    private readonly string _key;
    private readonly string? _oldValue;
    private readonly string? _newValue;

    public string Description => $"修改 {_key}: \"{_oldValue}\" → \"{_newValue}\"";

    public PropertyEdit(IWidgetEditorService editor, WidgetInstance widget,
        string key, string? oldValue, string? newValue)
    {
        _editor = editor;
        _widget = widget;
        _key = key;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Undo() => _editor.UpdateProperty(_widget, _key, _oldValue);
    public void Redo() => _editor.UpdateProperty(_widget, _key, _newValue);
}

/// <summary>添加控件编辑。</summary>
public sealed class AddWidgetEdit : IRevertibleEdit
{
    private readonly IWidgetEditorService _editor;
    private readonly PageDefinition _page;
    private readonly WidgetInstance _widget;

    public string Description => $"添加控件 [{_widget.TypeId}]";

    public AddWidgetEdit(IWidgetEditorService editor, PageDefinition page, WidgetInstance widget)
    {
        _editor = editor;
        _page = page;
        _widget = widget;
    }

    public void Undo() => _editor.RemoveWidget(_page, _widget.Id);
    public void Redo()
    {
        if (!_page.Widgets.Contains(_widget))
            _page.Widgets.Add(_widget);
    }
}

/// <summary>删除控件编辑。</summary>
public sealed class RemoveWidgetEdit : IRevertibleEdit
{
    private readonly IWidgetEditorService _editor;
    private readonly PageDefinition _page;
    private readonly WidgetInstance _widget;
    private readonly int _index;

    public string Description => $"删除控件 [{_widget.TypeId}]";

    public RemoveWidgetEdit(IWidgetEditorService editor, PageDefinition page, WidgetInstance widget, int index)
    {
        _editor = editor;
        _page = page;
        _widget = widget;
        _index = index;
    }

    public void Undo()
    {
        _page.Widgets.Insert(Math.Min(_index, _page.Widgets.Count), _widget);
    }

    public void Redo() => _editor.RemoveWidget(_page, _widget.Id);
}

/// <summary>可撤销编辑栈，支持 Undo/Redo。</summary>
public sealed class EditStack
{
    private readonly Stack<IRevertibleEdit> _undoStack = new();
    private readonly Stack<IRevertibleEdit> _redoStack = new();
    private readonly int _maxSize;

    public EditStack(int maxSize = 100)
    {
        _maxSize = maxSize;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>当前栈顶的描述（用于 UI 提示）。</summary>
    public string? UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>执行一个编辑操作，并将它压入撤销栈。</summary>
    public void Execute(IRevertibleEdit edit)
    {
        _undoStack.Push(edit);
        _redoStack.Clear(); // 新操作破坏 redo 链

        if (_undoStack.Count > _maxSize)
        {
            // 丢弃最旧的编辑
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (var i = _maxSize - 1; i >= 0; i--)
                _undoStack.Push(items[i]);
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var edit = _undoStack.Pop();
        edit.Undo();
        _redoStack.Push(edit);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var edit = _redoStack.Pop();
        edit.Redo();
        _undoStack.Push(edit);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
