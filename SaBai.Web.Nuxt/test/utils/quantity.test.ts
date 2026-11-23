import { formatQuantity, quantityUnit } from '~/utils/quantity'

describe('formatQuantity', () => {
  it('puts the unit after the number', () => {
    expect(formatQuantity(10, 'tablet')).toBe('10 tablets')
    expect(formatQuantity(1, 'tablet')).toBe('1 tablet')
    expect(formatQuantity(0, 'tablet')).toBe('0 tablets')
    expect(formatQuantity(2, 'bottle')).toBe('2 bottles')
  })

  it('leaves measures and unknown units as written', () => {
    expect(formatQuantity(5, 'mL')).toBe('5 mL')
    expect(formatQuantity(250, 'mg')).toBe('250 mg')
    expect(formatQuantity(3, 'g')).toBe('3 g')
    expect(formatQuantity(2, 'IU')).toBe('2 IU')
    expect(formatQuantity(4, 'blister of 10')).toBe('4 blister of 10')
  })

  it('uses irregular plurals', () => {
    expect(formatQuantity(2, 'box')).toBe('2 boxes')
    expect(formatQuantity(3, 'patch')).toBe('3 patches')
    expect(formatQuantity(6, 'suppository')).toBe('6 suppositories')
  })

  it('accepts a unit stored in the plural', () => {
    expect(formatQuantity(1, 'tablets')).toBe('1 tablet')
    expect(formatQuantity(12, 'tablets')).toBe('12 tablets')
    expect(formatQuantity(1, 'boxes')).toBe('1 box')
  })

  it('keeps the capitalisation and trims spaces', () => {
    expect(formatQuantity(2, 'Tablet')).toBe('2 Tablets')
    expect(formatQuantity(2, 'TABLET')).toBe('2 TABLETS')
    expect(formatQuantity(1, ' Capsules ')).toBe('1 Capsule')
  })

  it('shows the number alone when the unit is unknown', () => {
    expect(formatQuantity(10)).toBe('10')
    expect(formatQuantity(10, null)).toBe('10')
    expect(formatQuantity(10, '  ')).toBe('10')
  })

  it('shows a dash for a missing quantity', () => {
    expect(formatQuantity(null, 'tablet')).toBe('-')
    expect(formatQuantity(undefined, 'tablet')).toBe('-')
    expect(formatQuantity(Number.NaN, 'tablet')).toBe('-')
  })
})

describe('quantityUnit', () => {
  it('follows the quantity', () => {
    expect(quantityUnit('tablet', 1)).toBe('tablet')
    expect(quantityUnit('tablet', 2)).toBe('tablets')
    expect(quantityUnit('mL', 2)).toBe('mL')
  })

  it('is plural while no quantity is entered', () => {
    expect(quantityUnit('tablet')).toBe('tablets')
    expect(quantityUnit('tablet', null)).toBe('tablets')
  })

  it('is empty without a unit', () => {
    expect(quantityUnit(undefined, 3)).toBe('')
    expect(quantityUnit('', 3)).toBe('')
  })
})
