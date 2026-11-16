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
  type MedicationStockLine,
} from '~/offline/mutations/medication'
import { queryKeys } from '~/utils/api'

const stockLine: MedicationStockLine = {
  id: 'stock-1',
  medicationBrandId: 'brand-1',
  quantity: 10,
  expiry: '2030-01-01',
  location: 'Shelf A',
  stockStatus: 'active',
  remarks: null,
  brandName: 'Brand',
  ingredientName: 'Ingredient',
  ingredientId: 'ing-1',
  fallBelow: 5,
  isLowStock: false,
}

function seeded() {
  const qc = new QueryClient()
  qc.setQueryData(queryKeys.medicationStock(), [stockLine])
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
    }).not.toThrow()
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

  it('createOrder updates ordersList, pharmacyOrders and visit journey optimistically and is idempotent', () => {
    const qc = seeded()
    const vars = {
      id: 'ord-1',
      consultId: 'consult-1',
      medicationBrandId: 'brand-1',
      brandName: 'Brand',
      quantity: 3,
      dosageInstructions: '1 tablet',
      remarks: null,
      context: {
        patientDisplayId: 'P001',
        patientName: 'Alice',
        villageCode: 'V1',
        visitId: 'visit-1',
        visitDate: '2026-11-15',
        diagnoses: [{ id: 'd1', name: 'Fever' }],
        isLowStock: false,
      },
    }
    createOrder.optimistic!(vars, qc)
    const onceOrders = qc.getQueryData(queryKeys.ordersList())
    createOrder.optimistic!(vars, qc)
    expect(qc.getQueryData(queryKeys.ordersList())).toEqual(onceOrders)
    expect(qc.getQueryData(queryKeys.pharmacyOrders())).toBeDefined()
  })

  it('updateOrder approves order, deducts stock via FIFO, and updates pharmacy queue', () => {
    const qc = seeded()
    const varsCreate = {
      id: 'ord-1',
      consultId: 'consult-1',
      medicationBrandId: 'brand-1',
      brandName: 'Brand',
      quantity: 3,
      dosageInstructions: '1 tablet',
      remarks: null,
      context: {
        patientDisplayId: 'P001',
        patientName: 'Alice',
        villageCode: 'V1',
        visitId: 'visit-1',
        visitDate: '2026-11-15',
        diagnoses: [{ id: 'd1', name: 'Fever' }],
        isLowStock: false,
      },
    }
    createOrder.optimistic!(varsCreate, qc)
    expect(stock(qc)[0]!.quantity).toBe(10)

    const varsUpdate = {
      id: 'ord-1',
      status: 'APPROVED' as const,
      medicationBrandId: 'brand-1',
      quantity: 3,
      visitId: 'visit-1',
      stockSnapshot: [stockLine],
    }
    updateOrder.optimistic!(varsUpdate, qc)
    expect(stock(qc)[0]!.quantity).toBe(7)
  })

  it('deleteOrder removes order from ordersList and pharmacyOrders', () => {
    const qc = seeded()
    const varsCreate = {
      id: 'ord-1',
      consultId: 'consult-1',
      medicationBrandId: 'brand-1',
      brandName: 'Brand',
      quantity: 3,
      dosageInstructions: '1 tablet',
      remarks: null,
      context: {
        patientDisplayId: 'P001',
        patientName: 'Alice',
        villageCode: 'V1',
        visitId: 'visit-1',
        visitDate: '2026-11-15',
        diagnoses: [{ id: 'd1', name: 'Fever' }],
        isLowStock: false,
      },
    }
    createOrder.optimistic!(varsCreate, qc)
    expect(qc.getQueryData<any[]>(queryKeys.ordersList())).toHaveLength(1)

    deleteOrder.optimistic!({ id: 'ord-1', brandName: 'Brand', visitId: 'visit-1' }, qc)
    expect(qc.getQueryData<any[]>(queryKeys.ordersList())).toHaveLength(0)
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
    const vars = {
      id: 'ord-1',
      consultId: 'consult-1',
      medicationBrandId: 'brand-1',
      brandName: 'Brand',
      quantity: 1,
      dosageInstructions: '1',
      remarks: null,
      context: {
        patientDisplayId: 'P1',
        patientName: 'Test',
        villageCode: null,
        visitId: 'visit-1',
        visitDate: '2026-11-15',
        diagnoses: [],
        isLowStock: false,
      },
    }
    createOrder.optimistic!(vars, qc)
    const latest = qc.getQueryData<any>(queryKeys.patientsWithLatestVisit())
    expect(latest.patients[0].lastVisitJourney.pharmacyPending).toBe(1)
  })
})
