module.exports = {
  preset: 'jest-expo',
  setupFiles: ['<rootDir>/test/unit/setup.ts'],
  testMatch: ['<rootDir>/test/unit/**/*.test.ts', '<rootDir>/test/unit/**/*.test.tsx'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
    '^@loto/api$': '<rootDir>/../shared/api/src/index.ts',
    '^@loto/api/queries$': '<rootDir>/../shared/api/src/queries/index.ts',
    '^@loto/core$': '<rootDir>/../shared/core/src/index.ts',
    '^react$': '<rootDir>/node_modules/react',
    '^react/jsx-runtime$': '<rootDir>/node_modules/react/jsx-runtime.js',
    '^@tanstack/react-query$': '<rootDir>/node_modules/@tanstack/react-query',
  },
  transformIgnorePatterns: [
    'node_modules/(?!(.pnpm|((jest-)?react-native|@react-native(-community)?|expo(nent)?|@expo(nent)?/.*|@react-native-async-storage/.*|nativewind|react-native-css-interop|@tanstack/.*)))',
  ],
};
