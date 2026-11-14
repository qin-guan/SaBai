import { QueryClient } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'
import { getOfflineMutation } from '~/offline/defineOfflineMutation'
import {
  adjustMedicationStock,
  createMedicationBrand,
  createMedicationStock,
  splitMedicationStock,
  updateMedicationIngredient,
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
      'medication.stock.create', 'medication.stock.update', 'medication.stock.adjust', 'medication.stock.split',
    ]) {
      expect(getOfflineMutation(key), key).toBeDefined()
    }
  })

  it('adjusting sets the resulting quantity, however many times the edit is re-applied', () => {
    const qc = seeded()
    const vars = {
      adjustmentId: 'adj-1', stockId: 'stock-1', quantityChange: -3, resultingQuantity: 7,
      remarks: null, brandName: 'Brand', location: 'Shelf A',
    }
    adjustMedicationStock.optimistic!(vars, qc)
    adjustMedicationStock.optimistic!(vars, qc)
    adjustMedicationStock.optimistic!(vars, qc)
    expect(stock(qc)[0]!.quantity).toBe(7)
  })

  it('two queued adjustments end at the last one, re-applied in order any number of times', () => {
    const qc = seeded()
    const first = { adjustmentId: 'a1', stockId: 'stock-1', quantityChange: -3, resultingQuantity: 7, remarks: null, brandName: 'Brand', location: 'Shelf A' }
    const second = { adjustmentId: 'a2', stockId: 'stock-1', quantityChange: -2, resultingQuantity: 5, remarks: null, brandName: 'Brand', location: 'Shelf A' }
    for (let i = 0; i < 3; i++) {
      adjustMedicationStock.optimistic!(first, qc)
      adjustMedicationStock.optimistic!(second, qc)
    }
    expect(stock(qc)[0]!.quantity).toBe(5)
  })

  it('an adjustment to zero removes the line and does not reappear', () => {
    const qc = seeded()
    const vars = { adjustmentId: 'a', stockId: 'stock-1', quantityChange: -10, resultingQuantity: 0, remarks: null, brandName: 'Brand', location: 'Shelf A' }
    adjustMedicationStock.optimistic!(vars, qc)
    adjustMedicationStock.optimistic!(vars, qc)
    expect(stock(qc)).toEqual([])
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
      adjustMedicationStock.optimistic!({ adjustmentId: 'a', stockId: 's', quantityChange: 1, resultingQuantity: 2, remarks: null, brandName: 'B', location: 'L' }, qc)
      updateMedicationIngredient.optimistic!({ id: 'i', name: 'n', unitOfMeasurement: 'u', fallBelow: 1, remarks: null }, qc)
    }).not.toThrow()
  })
})
