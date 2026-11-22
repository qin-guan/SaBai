import { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'
import { getOfflineMutation } from '~/offline/defineOfflineMutation'
import {
  createMedicationBrand,
  createMedicationStock,
  splitMedicationStock,
  updateMedicationIngredient,
  createOrder,
  updateOrder,
  deleteOrder,
  orderSnapshot,
  type CreateOrderVars,
  type IngredientAvailability,
  type MedicationOrder,
  type MedicationStockLine,
  type PharmacyPatientOrders,
} from '~/offline/mutations/medication'
import { queryKeys } from '~/utils/api'
import { hasPendingDelta } from '~/utils/medicationCache'

const stockLine: MedicationStockLine = {
  id: 'stock-1',
  medicationBrandId: 'brand-1',
  quantity: 10,
  reservedQuantity: 0,
  availableQuantity: 10,
  expiry: '2030-01-01',
  location: 'Shelf A',
  stockStatus: 'active',
  remarks: null,
  parentStockId: null,
  brandName: 'Brand',
  ingredientName: 'Ingredient',
  ingredientId: 'ing-1',
  fallBelow: 5,
  isLowStock: false,
}

const availabilityRows: IngredientAvailability[] = [
  { ingredientId: 'ing-1', ingredientName: 'Ingredient', unitOfMeasurement: 'tablet', availableQuantity: 10 },
  { ingredientId: 'ing-2', ingredientName: 'Other', unitOfMeasurement: 'ml', availableQuantity: 50 },
]

function seeded() {
  const qc = new QueryClient()
  qc.setQueryData(queryKeys.medicationStock(), [stockLine])
  qc.setQueryData(queryKeys.medicationAvailability(), availabilityRows)
  qc.setQueryData(queryKeys.medicationIngredients(), [
    { id: 'ing-1', name: 'Ingredient', unitOfMeasurement: 'mg', fallBelow: 5, remarks: null },
  ])
  qc.setQueryData(queryKeys.medicationBrands(), [
    { id: 'brand-1', name: 'Brand', activeIngredientId: 'ing-1', remarks: null },
  ])
  qc.setQueryData(queryKeys.medicationBrandsWithIngredient(), [
    { id: 'brand-1', name: 'Brand', activeIngredientName: 'Ingredient' },
  ])
  return qc
}

const stock = (qc: QueryClient) => qc.getQueryData<MedicationStockLine[]>(queryKeys.medicationStock())!
const available = (qc: QueryClient, ingredientId = 'ing-1') =>
  qc.getQueryData<IngredientAvailability[]>(queryKeys.medicationAvailability())!.find(r => r.ingredientId === ingredientId)!.availableQuantity
const orders = (qc: QueryClient) => qc.getQueryData<MedicationOrder[]>(queryKeys.ordersList()) ?? []
const queue = (qc: QueryClient) => qc.getQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders()) ?? []

const createVars: CreateOrderVars = {
  id: 'ord-1',
  consultId: 'consult-1',
  medicationActiveIngredientId: 'ing-1',
  ingredientName: 'Ingredient',
  quantity: 3,
  dosageInstructions: '1 tablet',
  remarks: null,
  context: {
    patientDisplayId: 'P001',
    patientName: 'Alice',
    villageCode: 'V1',
    visitId: 'visit-1',
    visitDate: '2026-11-15',
    diagnoses: [{ category: 'Infectious', details: 'Fever' }],
    isLowStock: false,
  },
}

/** A pending order as the server lists it, holding 3 units of stock-1 (which then has 3 reserved). */
const placedOrder: MedicationOrder = {
  id: 'ord-1',
  consultId: 'consult-1',
  medicationActiveIngredientId: 'ing-1',
  activeIngredientName: 'Ingredient',
  quantity: 3,
  status: 'PENDING',
  dosageInstructions: '1 tablet',
  remarks: null,
  dispensedById: null,
  dispensedAt: null,
  createdAt: '2026-11-15T00:00:00Z',
  allocations: [{ stockId: 'stock-1', medicationBrandId: 'brand-1', brandName: 'Brand', location: 'Shelf A', expiry: '2030-01-01', quantity: 3 }],
}

/** The server's state after `placedOrder`: the order listed and in the queue, its units reserved. */
function withPlacedOrder() {
  const qc = seeded()
  qc.setQueryData(queryKeys.medicationStock(), [{ ...stockLine, reservedQuantity: 3, availableQuantity: 7 }])
  qc.setQueryData(queryKeys.medicationAvailability(), availabilityRows.map(r => r.ingredientId === 'ing-1' ? { ...r, availableQuantity: 7 } : r))
  qc.setQueryData(queryKeys.ordersList(), [placedOrder])
  qc.setQueryData(queryKeys.ordersList('PENDING'), [placedOrder])
  qc.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), [{
    patient: { displayId: 'P001', name: 'Alice', villageCode: 'V1' },
    visits: [{
      visitId: 'visit-1',
      visitDate: '2026-11-15',
      diagnoses: [],
      orders: [{ orderId: 'ord-1', medicationName: 'Ingredient', quantity: 3, isLowStock: false, dosageInstructions: '1 tablet', allocations: placedOrder.allocations }],
    }],
  }])
  return qc
}

/** Runs an optimistic edit twice, as the offline layer may, and returns the query client. */
function applyTwice<V>(edit: (vars: V, qc: QueryClient) => void, vars: V, qc: QueryClient) {
  edit(vars, qc)
  edit(vars, qc)
  return qc
}

describe('medication offline writes', () => {
  it('registers every definition by key', () => {
    for (const key of [
      'medication.ingredient.create', 'medication.ingredient.update', 'medication.ingredient.delete',
      'medication.brand.create', 'medication.brand.update', 'medication.brand.delete',
      'medication.stock.create', 'medication.stock.update', 'medication.stock.split',
    ]) {
      expect(getOfflineMutation(key), key).toBeDefined()
    }
  })

  it('splitting is idempotent and builds the children from the snapshot', () => {
    const qc = seeded()
    const vars = {
      stockId: 'stock-1',
      parent: stockLine,
      splits: [
        { id: 's1', quantity: 4, location: 'A', stockStatus: 'active' as const, remarks: null },
        { id: 's2', quantity: 6, location: 'B', stockStatus: 'active' as const, remarks: null },
      ],
    }
    splitMedicationStock.optimistic!(vars, qc)
    const once = stock(qc)
    splitMedicationStock.optimistic!(vars, qc)
    expect(stock(qc)).toEqual(once)
    expect(once.map(r => r.id).sort()).toEqual(['s1', 's2'])
  })

  it('a stock lot created offline appears in the stock list once', () => {
    const qc = seeded()
    const vars = {
      id: 'new-lot', medicationBrandId: 'brand-1', brandName: 'Brand', ingredientId: 'ing-1', ingredientName: 'Ingredient',
      fallBelow: 5, quantity: 3, expiry: '2031-01-01', location: 'Shelf B', stockStatus: 'active' as const, remarks: null,
    }
    createMedicationStock.optimistic!(vars, qc)
    createMedicationStock.optimistic!(vars, qc)
    expect(stock(qc).filter(r => r.id === 'new-lot')).toHaveLength(1)
  })

  it('a brand created offline shows in the brand lists', () => {
    const qc = seeded()
    const vars = { id: 'brand-2', name: 'New', activeIngredientId: 'ing-1', remarks: '', ingredientName: 'Ingredient', ingredientFallBelow: 5 }
    createMedicationBrand.optimistic!(vars, qc)
    createMedicationBrand.optimistic!(vars, qc)
    expect(qc.getQueryData<unknown[]>(queryKeys.medicationBrands())).toHaveLength(2)
    expect(qc.getQueryData<unknown[]>(queryKeys.medicationBrandsWithIngredient())).toHaveLength(2)
  })

  it('renaming an ingredient reaches brand and stock lists', () => {
    const qc = seeded()
    updateMedicationIngredient.optimistic!(
      { id: 'ing-1', name: 'Renamed', unitOfMeasurement: 'mg', fallBelow: 20, remarks: null },
      qc,
    )
    expect(stock(qc)[0]).toMatchObject({ ingredientName: 'Renamed', fallBelow: 20, isLowStock: true })
    expect(qc.getQueryData<{ activeIngredientName: string }[]>(queryKeys.medicationBrandsWithIngredient())![0]!.activeIngredientName).toBe('Renamed')
  })

  it('edits are safe when no query is cached', () => {
    const qc = new QueryClient()
    expect(() => {
      splitMedicationStock.optimistic!({ stockId: 's', parent: stockLine, splits: [] }, qc)
      updateMedicationIngredient.optimistic!({ id: 'i', name: 'n', unitOfMeasurement: 'u', fallBelow: 1, remarks: null }, qc)
      createOrder.optimistic!({ ...createVars, context: undefined }, qc)
      updateOrder.optimistic!({ id: 'ord-1', changeId: 'c', status: 'CANCELLED', before: orderSnapshot(placedOrder) }, qc)
      deleteOrder.optimistic!({ id: 'ord-1', before: { ...orderSnapshot(placedOrder), status: 'PENDING' } }, qc)
    }).not.toThrow()
  })

  it('splitting a batch with reservations keeps the reserved units in the parent', () => {
    const qc = withPlacedOrder()
    const parent = stock(qc)[0]!
    splitMedicationStock.optimistic!({
      stockId: parent.id,
      parent,
      splits: [
        { id: 's1', quantity: 3, location: 'A', stockStatus: 'active', remarks: null },
        { id: 's2', quantity: 4, location: 'B', stockStatus: 'active', remarks: null },
      ],
    }, qc)
    const byId = new Map(stock(qc).map(r => [r.id, r]))
    expect(byId.get('stock-1')).toMatchObject({ quantity: 3, reservedQuantity: 3, availableQuantity: 0 })
    expect(byId.get('s1')).toMatchObject({ quantity: 3, reservedQuantity: 0, availableQuantity: 3, parentStockId: 'stock-1' })
    expect(byId.get('s2')).toMatchObject({ quantity: 4, availableQuantity: 4 })
  })

  it('registers order mutations by key', () => {
    for (const key of [
      'medication.order.create',
      'medication.order.update',
      'medication.order.delete',
    ]) {
      expect(getOfflineMutation(key), key).toBeDefined()
    }
  })

  it('createOrder lists the pending order, queues it for the pharmacy and reserves its units once', () => {
    const qc = applyTwice(createOrder.optimistic!, createVars, seeded())
    expect(orders(qc)).toHaveLength(1)
    expect(orders(qc)[0]).toMatchObject({ id: 'ord-1', status: 'PENDING', activeIngredientName: 'Ingredient', allocations: [] })
    expect(qc.getQueryData<MedicationOrder[]>(queryKeys.ordersList('PENDING'))).toHaveLength(1)
    expect(queue(qc)[0]!.visits[0]!.orders).toHaveLength(1)
    expect(available(qc)).toBe(7)
    expect(available(qc, 'ing-2')).toBe(50)
    // Which batches are reserved is up to the server; the stock list is left as it is.
    expect(stock(qc)[0]).toMatchObject({ quantity: 10, reservedQuantity: 0 })
  })

  it('createOrder applies once more on fresh server data, never twice on the same data', () => {
    const qc = seeded()
    createOrder.optimistic!(createVars, qc)
    // A refetch while the order is still queued: the server has not reserved anything yet.
    qc.setQueryData(queryKeys.medicationAvailability(), availabilityRows.map(r => ({ ...r, availableQuantity: r.ingredientId === 'ing-1' ? 9 : r.availableQuantity })))
    applyTwice(createOrder.optimistic!, createVars, qc)
    expect(available(qc)).toBe(6)
  })

  it('createOrder revert takes the order and its reservation back', () => {
    const qc = seeded()
    createOrder.optimistic!(createVars, qc)
    createOrder.revert!(createVars, qc)
    expect(orders(qc)).toHaveLength(0)
    expect(queue(qc)).toHaveLength(0)
    const row = qc.getQueryData<IngredientAvailability[]>(queryKeys.medicationAvailability())!.find(r => r.ingredientId === 'ing-1')!
    expect(row.availableQuantity).toBe(10)
    expect(hasPendingDelta(row)).toBe(false)
  })

  it('dispensing takes the reserved units off hand without changing what is available', () => {
    const qc = withPlacedOrder()
    const vars = { id: 'ord-1', changeId: 'c1', status: 'DISPENSED' as const, before: orderSnapshot(placedOrder), visitId: 'visit-1', dispensedAt: '2026-11-15T01:00:00Z' }
    applyTwice(updateOrder.optimistic!, vars, qc)
    expect(stock(qc)[0]).toMatchObject({ quantity: 7, reservedQuantity: 0, availableQuantity: 7 })
    expect(available(qc)).toBe(7)
    expect(orders(qc)[0]).toMatchObject({ status: 'DISPENSED', dispensedAt: '2026-11-15T01:00:00Z', allocations: placedOrder.allocations })
    expect(qc.getQueryData<MedicationOrder[]>(queryKeys.ordersList('PENDING'))).toHaveLength(0)
    expect(queue(qc)).toHaveLength(0)
  })

  it('cancelling releases the reservation once and clears the allocations', () => {
    const qc = withPlacedOrder()
    const vars = { id: 'ord-1', changeId: 'c1', status: 'CANCELLED' as const, before: orderSnapshot(placedOrder) }
    applyTwice(updateOrder.optimistic!, vars, qc)
    expect(stock(qc)[0]).toMatchObject({ quantity: 10, reservedQuantity: 0, availableQuantity: 10 })
    expect(available(qc)).toBe(10)
    expect(orders(qc)[0]).toMatchObject({ status: 'CANCELLED', allocations: [] })
    expect(queue(qc)).toHaveLength(0)
  })

  it('a new quantity moves the reservation by the difference', () => {
    const qc = withPlacedOrder()
    const vars = { id: 'ord-1', changeId: 'c1', quantity: 5, before: orderSnapshot(placedOrder) }
    applyTwice(updateOrder.optimistic!, vars, qc)
    expect(available(qc)).toBe(5)
    expect(orders(qc)[0]).toMatchObject({ quantity: 5, status: 'PENDING', allocations: [] })
    // The old batches are released until the server re-allocates.
    expect(stock(qc)[0]).toMatchObject({ reservedQuantity: 0, availableQuantity: 10 })
    expect(queue(qc)[0]!.visits[0]!.orders[0]).toMatchObject({ quantity: 5, allocations: [] })

    // A second queued change of the same order counts on top of the first.
    updateOrder.optimistic!({ id: 'ord-1', changeId: 'c2', quantity: 4, before: { ...orderSnapshot(placedOrder), quantity: 5, allocations: [] } }, qc)
    expect(available(qc)).toBe(6)
  })

  it('a new ingredient releases the old one and reserves the new one', () => {
    const qc = withPlacedOrder()
    applyTwice(updateOrder.optimistic!, {
      id: 'ord-1',
      changeId: 'c1',
      medicationActiveIngredientId: 'ing-2',
      ingredientName: 'Other',
      quantity: 20,
      before: orderSnapshot(placedOrder),
    }, qc)
    expect(available(qc)).toBe(10)
    expect(available(qc, 'ing-2')).toBe(30)
    expect(orders(qc)[0]).toMatchObject({ medicationActiveIngredientId: 'ing-2', activeIngredientName: 'Other', quantity: 20 })
    expect(queue(qc)[0]!.visits[0]!.orders[0]).toMatchObject({ medicationName: 'Other', quantity: 20 })
  })

  it('dosage and remarks changes leave stock alone', () => {
    const qc = withPlacedOrder()
    updateOrder.optimistic!({ id: 'ord-1', changeId: 'c1', dosageInstructions: '2 tablets', remarks: 'after food', before: orderSnapshot(placedOrder) }, qc)
    expect(available(qc)).toBe(7)
    expect(stock(qc)[0]).toMatchObject({ reservedQuantity: 3 })
    expect(orders(qc)[0]).toMatchObject({ dosageInstructions: '2 tablets', remarks: 'after food', allocations: placedOrder.allocations })
  })

  it('deleteOrder removes a pending order and releases its reservation', () => {
    const qc = withPlacedOrder()
    applyTwice(deleteOrder.optimistic!, { id: 'ord-1', before: { ...orderSnapshot(placedOrder), status: 'PENDING' as const }, visitId: 'visit-1' }, qc)
    expect(orders(qc)).toHaveLength(0)
    expect(queue(qc)).toHaveLength(0)
    expect(available(qc)).toBe(10)
    expect(stock(qc)[0]).toMatchObject({ reservedQuantity: 0, availableQuantity: 10 })
  })

  it('deleting a cancelled order changes no stock', () => {
    const qc = seeded()
    qc.setQueryData(queryKeys.ordersList(), [{ ...placedOrder, status: 'CANCELLED', allocations: [] }])
    deleteOrder.optimistic!({ id: 'ord-1', before: { ...orderSnapshot(placedOrder), allocations: [], status: 'CANCELLED' as const } }, qc)
    expect(orders(qc)).toHaveLength(0)
    expect(available(qc)).toBe(10)
  })

  it('adjustVisitPharmacyJourney updates patientsWithLatestVisit shape correctly', () => {
    const qc = new QueryClient()
    qc.setQueryData(queryKeys.patientsWithLatestVisit(), {
      patients: [
        {
          id: 'patient-1',
          lastVisitId: 'visit-1',
          lastVisitJourney: { pharmacyPending: 0, pharmacyOrders: 0 },
        },
      ],
    })
    applyTwice(createOrder.optimistic!, createVars, qc)
    const latest = qc.getQueryData<{ patients: { lastVisitJourney: { pharmacyPending: number, pharmacyOrders: number } }[] }>(queryKeys.patientsWithLatestVisit())!
    expect(latest.patients[0]!.lastVisitJourney).toMatchObject({ pharmacyPending: 1, pharmacyOrders: 1 })

    updateOrder.optimistic!({ id: 'ord-1', changeId: 'c1', status: 'DISPENSED', before: orderSnapshot(placedOrder), visitId: 'visit-1', dispensedAt: '2026-11-15T01:00:00Z' }, qc)
    const after = qc.getQueryData<{ patients: { lastVisitJourney: { pharmacyPending: number, pharmacyAt?: string } }[] }>(queryKeys.patientsWithLatestVisit())!
    expect(after.patients[0]!.lastVisitJourney).toMatchObject({ pharmacyPending: 0, pharmacyAt: '2026-11-15T01:00:00Z' })
  })
})
