using System;
using System.Collections.Generic;
using SevenBattles.Core;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Players
{
    public sealed class UnitCatalogService : IUnitCatalog
    {
        private readonly UnitDefinitionRegistry _unitRegistry;

        public UnitCatalogService(UnitDefinitionRegistry unitRegistry)
        {
            _unitRegistry = unitRegistry;
        }

        public IReadOnlyList<UnitDefinition> GetAllUnits()
        {
            return _unitRegistry != null ? _unitRegistry.GetAll() : Array.Empty<UnitDefinition>();
        }

        public bool TryGetById(string unitId, out UnitDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            if (_unitRegistry != null)
            {
                definition = _unitRegistry.GetById(unitId);
                return definition != null;
            }

            return false;
        }
    }
}
