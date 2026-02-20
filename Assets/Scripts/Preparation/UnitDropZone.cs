using System;
using SevenBattles.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenBattles.Preparation
{
    public sealed class UnitDropZone : MonoBehaviour, IDropHandler
    {
        public enum ZoneType
        {
            AllUnits,
            ActiveSquad
        }

        [SerializeField] private ZoneType _zoneType;

        public ZoneType Type => _zoneType;

        public event Action<UnitSpellLoadout, ZoneType> DropReceived;

        public void SetZoneType(ZoneType zoneType)
        {
            _zoneType = zoneType;
        }

        public void OnDrop(PointerEventData eventData)
        {
            UnitSpellLoadout loadout = UnitDragHandler.DraggingLoadout;
            if (loadout == null)
            {
                return;
            }

            DropReceived?.Invoke(loadout, _zoneType);
        }
    }
}
