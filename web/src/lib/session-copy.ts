// pt-BR copy fixed by design 0.4.2 (`design/docs/screens.md`, cross-screen patterns) for the states
// this plumbing reports. Screens place it; backend `ErrorMessages` are always shown verbatim beside it.
export const sessionCopy = Object.freeze({
  resultNotConfirmed: 'Resultado não confirmado',
  confirmationWarning: 'Não foi possível confirmar se a ação foi concluída.',
  verifyAndRetry: 'Verificar e tentar novamente',
  noUsableErrorBody: 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
  renewalFailed: 'Não foi possível renovar sua sessão. Entre novamente.',
  sessionRenewing: 'Sua sessão está sendo renovada…',
  reloadAndReview: 'Recarregar e conferir',
  deviceDraft: 'Rascunho salvo neste dispositivo — ainda não salvo no servidor',
  deviceStorageFailed:
    'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.',
  branchDayChanged: 'A data da filial mudou. Confira os dados antes de continuar.',
  logoutUnconfirmed:
    'Você saiu deste dispositivo. Não foi possível confirmar o encerramento da sessão no servidor.',
});
