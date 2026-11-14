export interface LocalMedicationStock {
  id: string
  medicationBrandId: string
  brandName: string
  activeIngredientId: string
  activeIngredientName: string
  ingredientId: string
  ingredientName: string
  unitOfMeasurement: string
  quantity: number
  expiry: string | null
  location: string
  stockStatus: string
  remarks: string | null
  isLowStock: boolean
}

export interface LocalMedicationBrand {
  id: string
  name: string
  activeIngredientId: string
  remarks: string | null
}

export interface LocalMedicationBrandWithIngredient {
  id: string
  name: string
  activeIngredientId: string
  activeIngredientName: string
  unitOfMeasurement: string
  remarks: string | null
}

export interface LocalMedicationIngredient {
  id: string
  name: string
  unitOfMeasurement: string
  fallBelow: number | null
  remarks: string | null
}

export function useMedicationStock() {
  return useLocalQuery<LocalMedicationStock[]>(
    ['medication_stocks', 'medication_brands', 'medication_active_ingredients'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<{
        id: string
        medication_brand_id: string
        brand_name: string
        active_ingredient_id: string
        active_ingredient_name: string
        unit_of_measurement: string
        fall_below: number | null
        quantity: number
        expiry: string | null
        location: string
        stock_status: string
        remarks: string | null
      }>(
        `SELECT
           ms.id,
           ms.medication_brand_id,
           mb.name AS brand_name,
           mb.active_ingredient_id,
           mai.name AS active_ingredient_name,
           mai.unit_of_measurement,
           mai.fall_below,
           ms.quantity,
           ms.expiry,
           ms.location,
           ms.stock_status,
           ms.remarks
         FROM medication_stocks ms
         JOIN medication_brands mb ON mb.id = ms.medication_brand_id
         JOIN medication_active_ingredients mai ON mai.id = mb.active_ingredient_id
         ORDER BY mai.name ASC, mb.name ASC`,
      )
      return rows.map((r) => {
        const qty = Number(r.quantity ?? 0)
        return {
          id: r.id,
          medicationBrandId: r.medication_brand_id,
          brandName: r.brand_name,
          activeIngredientId: r.active_ingredient_id,
          activeIngredientName: r.active_ingredient_name,
          ingredientId: r.active_ingredient_id,
          ingredientName: r.active_ingredient_name,
          unitOfMeasurement: r.unit_of_measurement,
          quantity: qty,
          expiry: r.expiry,
          location: r.location ?? 'Unassigned',
          stockStatus: r.stock_status ?? 'Available',
          remarks: r.remarks,
          isLowStock: r.fall_below != null && qty < Number(r.fall_below),
        }
      })
    },
  )
}

export function useMedicationBrands() {
  return useLocalQuery<LocalMedicationBrand[]>(
    ['medication_brands'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<{ id: string, name: string, active_ingredient_id: string, remarks: string | null }>(
        'SELECT id, name, active_ingredient_id, remarks FROM medication_brands ORDER BY name ASC',
      )
      return rows.map(r => ({
        id: r.id,
        name: r.name,
        activeIngredientId: r.active_ingredient_id,
        remarks: r.remarks,
      }))
    },
  )
}

export function useMedicationBrandsWithIngredient() {
  return useLocalQuery<LocalMedicationBrandWithIngredient[]>(
    ['medication_brands', 'medication_active_ingredients'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<{
        id: string
        name: string
        active_ingredient_id: string
        active_ingredient_name: string
        unit_of_measurement: string
        remarks: string | null
      }>(
        `SELECT mb.id, mb.name, mb.active_ingredient_id, mai.name AS active_ingredient_name, mai.unit_of_measurement, mb.remarks
         FROM medication_brands mb
         JOIN medication_active_ingredients mai ON mai.id = mb.active_ingredient_id
         ORDER BY mb.name ASC`,
      )
      return rows.map(r => ({
        id: r.id,
        name: r.name,
        activeIngredientId: r.active_ingredient_id,
        activeIngredientName: r.active_ingredient_name,
        unitOfMeasurement: r.unit_of_measurement,
        remarks: r.remarks,
      }))
    },
  )
}

export function useMedicationIngredients() {
  return useLocalQuery<LocalMedicationIngredient[]>(
    ['medication_active_ingredients'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<{
        id: string
        name: string
        unit_of_measurement: string
        fall_below: number | null
        remarks: string | null
      }>(
        'SELECT id, name, unit_of_measurement, fall_below, remarks FROM medication_active_ingredients ORDER BY name ASC',
      )
      return rows.map(r => ({
        id: r.id,
        name: r.name,
        unitOfMeasurement: r.unit_of_measurement,
        fallBelow: r.fall_below,
        remarks: r.remarks,
      }))
    },
  )
}
