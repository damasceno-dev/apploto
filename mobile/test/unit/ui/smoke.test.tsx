import { fireEvent, screen } from '@testing-library/react-native';
import { Button } from '../../../src/ui/button';
import { light, renderThemed, styleOf } from './support';

test('renders a primary button with token colors after the preference is read', async () => {
  const onPress = jest.fn();
  renderThemed(<Button label="Salvar" onPress={onPress} />);
  const button = await screen.findByRole('button', { name: 'Salvar' });
  expect(styleOf(button).backgroundColor).toBe(light.sem.action.primary.bg);
  fireEvent.press(button);
  expect(onPress).toHaveBeenCalledTimes(1);
});
