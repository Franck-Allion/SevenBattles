using System.Collections.Generic;
using SevenBattles.Core.Units;

namespace SevenBattles.Core
{
    /// <summary>
    /// Read-only access to static unit definitions.
    /// </summary>
    public interface IUnitCatalog
    {
        IReadOnlyList<UnitDefinition> GetAllUnits();
        bool TryGetById(string unitId, out UnitDefinition definition);
    }
}
