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
                EquipmentDropSlotView draggedSlot = eventData != null && eventData.pointerDrag != null
                    ? eventData.pointerDrag.GetComponentInParent<EquipmentDropSlotView>()
                    : null;
                IEquipmentService effectiveService = _equipmentService ?? draggedSlot?.EquipmentService;
                OwnedUnitData effectiveUnit = _selectedUnit ?? draggedSlot?.SelectedUnit;
                EquipmentSlotType? draggingFromSlot = EquipmentDropSlotView.DraggingFromSlot;
                if (!draggingFromSlot.HasValue && draggedSlot != null)
                {
                    draggingFromSlot = draggedSlot.SlotType;
                }

                if (effectiveService != null &&
                    effectiveUnit != null &&
                    draggingFromSlot.HasValue &&
                    effectiveService.TryUnequip(effectiveUnit, draggingFromSlot.Value))
                {
                    NotifyDraggedSlotAccepted(eventData);
                    if (_enableDiagnostics)
                    {
                        Core.Diagnostics.SBLog.Info(
                            $"InventoryDropZone: Equipment unequipped from slot '{draggingFromSlot.Value}' on unit '{effectiveUnit.OwnedUnitId}'.",
                            this);
                    }
                    return;
                }

                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Warn(
                        $"InventoryDropZone: Equipment unequip drop rejected (service={(effectiveService != null ? "yes" : "no")}, unit={(effectiveUnit != null ? effectiveUnit.OwnedUnitId : "<null>")}, slot={draggingFromSlot?.ToString() ?? "<null>"}, draggedSlot={(draggedSlot != null ? draggedSlot.SlotType.ToString() : "<null>")}).",
                        this);
                }
                DropReceived?.Invoke(this, eventData);
                return;
            }

            if (ConsumableDropSlotView.IsDraggingEquippedConsumable)
            {
                ConsumableDropSlotView draggedSlot = eventData != null && eventData.pointerDrag != null
                    ? eventData.pointerDrag.GetComponentInParent<ConsumableDropSlotView>()
                    : null;
                IItemEquipService effectiveService = _itemEquipService ?? draggedSlot?.ItemEquipService;
                OwnedUnitData effectiveUnit = _selectedUnit ?? draggedSlot?.SelectedUnit;
                ConsumableSlotType? draggingFromSlot = ConsumableDropSlotView.DraggingFromConsumableSlot;
                if (!draggingFromSlot.HasValue && draggedSlot != null)
                {
                    draggingFromSlot = draggedSlot.SlotType;
                }

                if (effectiveService != null &&
                    effectiveUnit != null &&
                    draggingFromSlot.HasValue &&
                    effectiveService.TryUnequip(effectiveUnit, draggingFromSlot.Value))
                {
                    NotifyDraggedConsumableSlotAccepted(eventData);
                    if (_enableDiagnostics)
                    {
                        Core.Diagnostics.SBLog.Info(
                            $"InventoryDropZone: Consumable unequipped from slot '{draggingFromSlot.Value}' on unit '{effectiveUnit.OwnedUnitId}'.",
                            this);
                    }
                    return;
                }

                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Warn(
                        $"InventoryDropZone: Consumable unequip drop rejected (service={(effectiveService != null ? "yes" : "no")}, unit={(effectiveUnit != null ? effectiveUnit.OwnedUnitId : "<null>")}, slot={draggingFromSlot?.ToString() ?? "<null>"}, draggedSlot={(draggedSlot != null ? draggedSlot.SlotType.ToString() : "<null>")}).",
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
