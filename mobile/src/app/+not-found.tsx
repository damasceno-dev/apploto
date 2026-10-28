import { Link } from 'expo-router';
import { Text, View } from 'react-native';

export default function NotFound() {
  return (
    <View className="flex-1 gap-4 bg-sem-background p-6">
      <Text
        accessibilityRole="header"
        className="text-section-title font-semibold text-sem-text-primary"
      >
        Página não encontrada
      </Text>
      <Link href="/" className="text-body text-sem-text-link underline">
        Voltar ao início
      </Link>
    </View>
  );
}
