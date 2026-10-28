/** Limit automation to the assigned development app or an explicitly named disposable probe. */
export function nativeAppId(environment = process.env) {
  const id = environment.LOTO_NATIVE_APP_ID ?? 'com.example.lotero.development';
  if (
    id !== 'com.example.lotero.development' &&
    !/^com\.example\.lotero\.nativeproof\.[a-z][a-z0-9]*$/.test(id)
  )
    throw new Error(
      'Native automation requires the development app or an owned nativeproof identifier.',
    );
  return id;
}
