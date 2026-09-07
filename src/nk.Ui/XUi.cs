using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Nokt;

namespace Nokt.Ui;

public sealed class XUi : IUiHost
{
    public void Show(UiWindowDefinition definition, Action<List<Statement>> execute)
    {
        ApplicationConfiguration.Initialize();

        using var window = new Form
        {
            Text = definition.Title,
            ClientSize = new Size(definition.Width, definition.Height),
            StartPosition = FormStartPosition.CenterScreen
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        foreach (UiElementDefinition element in definition.Elements)
        {
            switch (element)
            {
                case UiTextDefinition text:
                    layout.Controls.Add(new Label
                    {
                        AutoSize = true,
                        Text = text.Value,
                        Margin = new Padding(3, 3, 3, 12)
                    });
                    break;
                case UiInputDefinition input:
                    layout.Controls.Add(new TextBox
                    {
                        Width = Math.Max(240, definition.Width - 64),
                        PlaceholderText = input.Placeholder,
                        Margin = new Padding(3, 3, 3, 12)
                    });
                    break;
                case UiButtonDefinition buttonDefinition:
                    var button = new Button
                    {
                        AutoSize = true,
                        Text = buttonDefinition.Label,
                        Margin = new Padding(3, 3, 3, 12)
                    };
                    button.Click += (_, _) => execute(buttonDefinition.OnClick);
                    layout.Controls.Add(button);
                    break;
                default:
                    throw new NoktException("unknown xUI element");
            }
        }

        window.Controls.Add(layout);
        Application.Run(window);
    }
}
