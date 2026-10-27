export type DecimalString = string & { readonly __decimal: unique symbol };
export function decimalString(value: string): DecimalString {
  if (!/^-?[0-9]+(?:\.[0-9]+)?$/.test(value)) throw new Error('Invalid decimal string');
  return value as DecimalString;
}
// Format exact strings; never pass money through a JS Number.
export function formatMoney(value: DecimalString, currency: string, locale = 'vi-VN') {
  const [integer, fraction] = value.split('.');
  const decimal = new Intl.NumberFormat(locale)
    .formatToParts(1.1)
    .find((part) => part.type === 'decimal')!.value;
  const whole = BigInt(integer);
  const signedWhole = whole === 0n && integer.startsWith('-') ? -0 : whole;
  return `${new Intl.NumberFormat(locale).format(signedWhole)}${fraction ? decimal + fraction : ''} ${currency}`;
}
