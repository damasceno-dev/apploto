import { Link } from 'expo-router';
import { Text, View } from 'react-native';

export default function NotFound() {
  return (
    <View className="gap-4 p-6">
      <Text accessibilityRole="header">Página não encontrada</Text>
      <Link href="/">Voltar ao início</Link>
    </View>
  );
}
