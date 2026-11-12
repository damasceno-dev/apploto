'use client';
import { useEffect, useRef, useState } from 'react';
import { type RequestGoLiveImportJson } from '@loto/api';
import { useGetGoLiveImports } from '@loto/api/queries';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { controlChrome } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import {
  importStatusLabels,
  Picker,
  useCommand,
  type UploadMemory,
  type UploadManifest,
} from './go-live-controls';
import { GoLiveSetup } from './go-live-setup';
import { ManualOpening, PhysicalOpening } from './go-live-openings';
import { ImportEditor } from './go-live-import-editor';
export function GoLiveDesk({ today }: { readonly today: string }) {
  const client = useScopedApiClient();
  const imports = useGetGoLiveImports({ request: { client } });
  const [selected, setSelected] = useState('');
  const [profile, setProfile] = useState('mestre-1.5.0');
  const [encoding, setEncoding] = useState('utf-8-sig');
  const [files, setFiles] = useState<RequestGoLiveImportJson['files']>([]);
  const [fileError, setFileError] = useState('');
  const [manifest, setManifest] = useState<UploadManifest>([]);
  const sourceMemory = useRef<UploadMemory | null>(null);
  useEffect(() => {
    sourceMemory.current = files.length
      ? { request: { profile, encoding, files }, manifest }
      : null;
  }, [files, manifest, profile, encoding]);
  const upload = useCommand(
    'go-live-upload',
    (value) => {
      if ('sourceDigest' in value) {
        setSelected(value.id);
        setFiles([]);
        setManifest([]);
        sourceMemory.current = null;
        void imports.refetch();
      }
    },
    sourceMemory,
  );
  const readFiles = async (input: FileList | null) => {
    setFileError('');
    setFiles([]);
    setManifest([]);
    const chosen = Array.from(input ?? []).sort((a, b) =>
      a.name < b.name ? -1 : a.name > b.name ? 1 : 0,
    );
    if (
      chosen.length > 19 ||
      chosen.some((file) => file.size > 16 * 1024 * 1024) ||
      chosen.reduce((sum, file) => sum + file.size, 0) > 64 * 1024 * 1024
    ) {
      setFileError('Os arquivos excedem os limites da exportação.');
      return;
    }
    try {
      const read = await Promise.all(
        chosen.map(async (file) => {
          const bytes = await file.arrayBuffer();
          const digest = await crypto.subtle.digest('SHA-256', bytes);
          const sha256 = Array.from(new Uint8Array(digest), (byte) =>
            byte.toString(16).padStart(2, '0'),
          ).join('');
          const base64 = await new Promise<string>((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(String(reader.result).split(',')[1] ?? '');
            reader.onerror = reject;
            reader.readAsDataURL(new Blob([bytes]));
          });
          return { name: file.name, base64, sha256 };
        }),
      );
      setFiles(read.map(({ name, base64 }) => ({ name, base64 })));
      setManifest(read.map(({ name, sha256 }) => ({ name, sha256 })));
    } catch {
      setFileError('Não foi possível ler os arquivos. Selecione novamente.');
    }
  };
  return (
    <div className="flex flex-col gap-stack-lg">
      <Card title="Começar a usar a filial" variant="checklist">
        <p>
          Confira as contas, produtos, clientes e operadores. Registre as obrigações anteriores ou
          revise uma exportação antes de ativá-la.
        </p>
        <p>
          O saldo físico de implantação é conferido na abertura do primeiro turno. Zero exige
          confirmação explícita.
        </p>
      </Card>
      <GoLiveSetup today={today} />
      <PhysicalOpening today={today} />
      <ManualOpening today={today} />
      <Card title="Importar exportação do Access">
        <details>
          <summary>Como preparar e revisar a exportação</summary>
          <p>
            Selecione todos os CSV da pasta tables do export Windows (13 tabelas, CP1252) ou Mestre
            1.5.0 (19 tabelas, UTF-8). Não envie o banco Access, consultas, macros ou arquivos
            compactados.
          </p>
          <p>
            Limites: 16 MiB por arquivo, 64 MiB e 100 mil registros no conjunto. Preserve nomes e
            colunas. Datas: ano/mês/dia hora:minuto:segundo; decimais com ponto.
          </p>
          <p>
            Valores com precisão excedente, cliente, custódia ou ano desconhecidos exigem resolução
            explícita. Exclusões precisam de motivo. A prévia mantém o histórico pendente e não
            movimenta dinheiro.
          </p>
        </details>
        <Picker
          label="Versão da exportação"
          value={profile}
          options={[
            { value: 'windows', label: 'Export Windows' },
            { value: 'mestre-1.5.0', label: 'Mestre 1.5.0' },
          ]}
          onChange={setProfile}
        />
        <Picker
          label="Codificação"
          value={encoding}
          options={[
            { value: 'cp1252', label: 'Windows CP1252' },
            { value: 'utf-8-sig', label: 'UTF-8' },
          ]}
          onChange={setEncoding}
        />
        <FormField id="go-live-source-files" label="Tabelas CSV" error={fileError}>
          <input
            id="go-live-source-files"
            className={controlChrome}
            type="file"
            multiple
            accept=".csv"
            onChange={(event) => void readFiles(event.target.files)}
          />
        </FormField>
        <Button
          disabled={upload.busy || files.length === 0}
          onClick={() => {
            sourceMemory.current = { request: { profile, encoding, files }, manifest };
            void upload.run({ kind: 'upload', profile, encoding, manifest });
          }}
        >
          Enviar para revisão
        </Button>
        <p>
          Os arquivos ficam apenas na memória até o envio. Após atualizar a página, selecione os
          mesmos CSV para verificar um envio pendente; o registro do dispositivo guarda apenas nomes
          e resumos digitais.
        </p>
        {upload.feedback}
      </Card>
      <Card title="Importações da filial">
        {imports.isPending ? (
          <p>Carregando importações…</p>
        ) : imports.isError ? (
          <Notice kind="warning" title="Não foi possível consultar as importações.">
            <Button onClick={() => void imports.refetch()}>Tentar novamente</Button>
          </Notice>
        ) : (
          <Picker
            label="Retomar revisão"
            value={selected}
            options={(imports.data?.data ?? []).map((item) => ({
              value: item.id,
              label: `${item.profile} · revisão ${item.mappingRevision} · ${importStatusLabels[item.status] ?? item.status}`,
            }))}
            onChange={setSelected}
          />
        )}
      </Card>
      {selected && <ImportEditor key={selected} id={selected} today={today} />}
    </div>
  );
}
