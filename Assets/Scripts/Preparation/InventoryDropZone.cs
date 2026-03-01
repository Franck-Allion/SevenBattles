using System;
using SevenBattles.Core;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenBattles.Preparation
{
    public sealed class InventoryDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private bool _enableDiagnostics;
        private IEquipmentService _equipmentService;
        private IItemEquipService _itemEquipService;
        private OwnedUnitData _selectedUnit;

        public event Action<InventoryDropZone, PointerEventData> DropReceived;

        public void SetEquipmentService(IEquipmentService equipmentService)
        {
            _equipmentService = equipmentService;
        }

        public void SetItemEquipService(IItemEquipService itemEquipService)
        {
            _itemEquipService = itemEquipService;
        }

        public void SetSelectedUnit(OwnedUnitData selectedUnit)
        {
            _selectedUnit = selectedUnit;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (EquipmentDropSlotView.IsDraggingEquippedItem)
            {
                EquipmentSlotType? draggingFromSlot = EquipmentDropSlotView.DraggingFromSlot;
                if (_equipmentService != null &&
                    _selectedUnit != null &&
                    draggingFromSlot.HasValue &&
                    _equipmentService.TryUnequip(_selectedUnit, draggingFromSlot.Value))
                {
                    NotifyDraggedSlotAccepted(eventData);
                    return;
                }

                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Warn(
                        $"InventoryDropZone: Equipment unequip drop rejected (service={(_equipmentService != null ? "yes" : "no")}, unit={(_selectedUnit != null ? _selectedUnit.OwnedUnitId : "<null>")}, slot={draggingFromSlot?.ToString() ?? "<null>"}).",
                        this);
                }
                DropReceived?.Invoke(this, eventData);
                return;
            }

            if (ConsumableDropSlotView.IsDraggingEquippedConsumable)
            {
                ConsumableSlotType? draggingFromSlot = ConsumableDropSlotView.DraggingFromConsumableSlot;
                if (_itemEquipService != null &&
                    _selectedUnit != null &&
                    draggingFromSlot.HasValue &&
                    _itemEquipService.TryUnequip(_selectedUnit, draggingFromSlot.Value))
                {
                    NotifyDraggedConsumableSlotAccepted(eventData);
                    return;
                }

                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Warn(
                        $"InventoryDropZone: Consumable unequip drop rejected (service={(_itemEquipService != null ? "yes" : "no")}, unit={(_selectedUnit != null ? _selectedUnit.OwnedUnitId : "<null>")}, slot={draggingFromSlot?.ToString() ?? "<null>"}).",
                        this);
                }
                DropReceived?.Invoke(this, eventData);
                return;
            }

            if (_enableDiagnostics)
            {
                Core.Diagnostics.SBLog.Info("InventoryDropZone: OnDrop received but no supported drag source is active.", this);
            }
        }

        private static void NotifyDraggedSlotAccepted(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            EquipmentDropSlotView draggedSlot = eventData.pointerDrag.GetComponentInParent<EquipmentDropSlotView>();
            if (draggedSlot != null)
            {
                draggedSlot.NotifyDropAccepted();
            }
        }

        private static void NotifyDraggedConsumableSlotAccepted(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            ConsumableDropSlotView draggedSlot = eventData.pointerDrag.GetComponentInParent<ConsumableDropSlotView>();
            if (draggedSlot != null)
            {
                draggedSlot.NotifyDropAccepted();
            }
        }
    }
}
