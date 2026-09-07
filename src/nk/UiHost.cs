using System;
using System.Collections.Generic;

namespace Nokt;

public interface IUiHost
{
    void Show(UiWindowDefinition definition, Action<List<Statement>> execute);
}
