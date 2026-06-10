# Manual de validacao do piloto local

Este roteiro valida o fluxo principal do operador no Guardiao em ambiente local, cobrindo painel Web, API, worker, casos monitorados, cameras, incidentes e auditoria.

## 1. Preparacao do ambiente

1. Copie o arquivo de ambiente:

```bash
cp .env.example .env
```

2. Revise pelo menos estes valores no `.env`:
   - `WEB_ENABLE_OPERATIONS_DEMO_LOGIN=true`
   - `PANEL_API_SHARED_SECRET`
   - `WORKER_API_SHARED_SECRET`
   - `EDGE_CAMERA_0_SOURCE=webcam://0` para webcam local
   - `EDGE_CAMERA_0_ID`, `EDGE_CAMERA_0_SITE_ID` e `EDGE_CAMERA_0_PROTECTED_CASE_ID` coerentes com os seeds do piloto

3. Garanta que os modelos estejam presentes:
   - `models/haarcascade_frontalface_default.xml`
   - `models/face-embedding.onnx`

4. Suba a stack:

```bash
docker compose up --build -d
```

5. Execute a validacao rapida:

```bash
bash scripts/post-deploy-smoke.sh
```

## 2. Verificacoes tecnicas iniciais

Confirme que os tres processos respondem:

```bash
curl http://localhost:8080/health
curl http://localhost:8080/ready
curl http://localhost:8081/health
curl http://localhost:8081/ready
curl http://localhost:18081/health
curl http://localhost:18081/ready
curl http://localhost:18081/metrics
```

Resultado esperado:
- API com `Healthy` e `Ready`
- Web com `Healthy` e `Ready`
- Worker com `Healthy`, `Ready` e contadores aumentando em `/metrics`

Se a camera estiver ativa, espere crescimento em chaves como:
- `fps_in|camera=...`
- `fps_processed|camera=...`

## 3. Login do operador

1. Abra `http://localhost:8081/login`.
2. Use o login local do piloto:
   - usuario: `operator.ana`
   - perfil: `Operator`
3. Clique em `Iniciar sessao`.

Resultado esperado:
- redirecionamento para `/`
- menu lateral com `Resumo`, `Incidentes`, `Casos`, `Cameras` e `Auditoria`
- sessao exibindo `operator.ana`

## 4. Validacao da Home operacional

1. Abra `http://localhost:8081/`.
2. Verifique:
   - definicoes visuais de `Incidente` e `Casos monitorados`
   - cards de contagem
   - lista de cameras monitoradas
   - preview da ultima evidencia quando houver
   - mulheres detectadas no ambiente quando houver deteccao recente
   - alerta visual de agressor quando houver correlacao no mesmo contexto

Resultado esperado:
- a tela nao fica travada em `Carregando`
- sem evidencias, a Home continua renderizando com estados vazios claros
- com evidencias, a camera mostra snapshot recente e contexto operacional

## 5. Validacao de casos monitorados

1. Abra `http://localhost:8081/cases`.
2. Confirme que a lista exibe:
   - identificador externo
   - perfil operacional
   - status de monitoramento
   - versao
3. Clique em `Regras` em um caso.
4. Na tela de detalhe, valide:
   - classificacao operacional atual
   - seletor entre `Mulher protegida` e `Agressor monitorado`
   - secao de biometria cadastrada
   - secao de regras por camera

### Teste de atualizacao de classificacao

1. Altere o perfil do caso.
2. Clique em `Salvar classificacao`.

Resultado esperado:
- mensagem de sucesso
- valor atualizado ao recarregar a pagina
- novo evento correspondente em `Auditoria`

### Teste de biometria

1. Ainda no detalhe do caso, selecione uma imagem facial valida.
2. Clique em `Enviar imagem`.

Resultado esperado:
- o botao so habilita depois da selecao do arquivo
- a imagem vira template biometrico ativo
- o template aparece na tabela

## 6. Validacao de cameras

1. Abra `http://localhost:8081/cameras`.
2. Confirme que a tela mostra:
   - sites cadastrados
   - cameras associadas
   - origem do stream
   - estado atual `Ativa` ou `Inativa`

### Teste de toggle

1. Clique em `Desabilitar` em uma camera ativa.
2. Aguarde a atualizacao da tela.
3. Clique em `Habilitar` para voltar ao estado original.

Resultado esperado:
- estado visual atualizado na tela
- evento `camera.state.updated` registrado em `Auditoria`

## 7. Validacao do worker com webcam local

Este passo valida captura real da webcam no piloto.

1. Garanta que `EDGE_CAMERA_0_SOURCE=webcam://0`.
2. Se estiver rodando o worker fora do Docker, inicie:

```bash
dotnet run --project src/Guardiao.Worker.Edge
```

3. Se estiver rodando via Docker, garanta acesso ao dispositivo `/dev/video0` no host ou rode o worker localmente.
4. Verifique novamente:

```bash
curl http://localhost:18081/ready
curl http://localhost:18081/metrics
```

Resultado esperado:
- `enabledCameraCount` maior que zero
- contadores `fps_in` e `fps_processed` crescendo

Observacao:
- o painel nao exibe streaming ao vivo nesta fase
- a Home mostra preview da ultima evidencia gerada, nao um player continuo

## 8. Validacao de incidente

Ha duas formas de validar.

### Opcao A: deteccao operacional real

1. Cadastre biometria de um caso monitorado.
2. Posicione a pessoa correspondente diante da camera.
3. Aguarde a deteccao e a correlacao pelo worker.

Resultado esperado:
- a API recebe candidate events
- um incidente aparece em `Incidentes`
- a Home passa a mostrar contexto da camera e evidencias recentes

### Opcao B: injecao controlada para smoke funcional

Use um `curl` de candidate event compativel com o seed local e com o segredo tecnico do worker configurado.

Depois valide:
1. Abra `http://localhost:8081/incidents`.
2. Clique em `Abrir` no incidente criado.
3. Verifique:
   - dados do incidente
   - evidencias
   - historico de notificacoes quando existente

### Teste de revisao humana

1. No detalhe do incidente, clique em `Confirmar` ou `Descartar`.
2. Informe observacao operacional quando aplicavel.

Resultado esperado:
- status atualizado
- auditoria correspondente registrada
- incidente refletido na Home e na lista

## 9. Validacao da auditoria

1. Abra `http://localhost:8081/audit`.
2. Confirme a presenca de eventos gerados durante o roteiro:
   - login nao precisa aparecer
   - `protected_case.subject_role.updated`
   - `camera.state.updated`
   - `biometric_template.created`
   - `incident.review.confirmed` ou `incident.review.dismissed`

Resultado esperado:
- a tela renderiza sem travar
- os registros mais recentes aparecem no topo

## 10. Validacao de logout

1. Clique em `Encerrar sessao`.

Resultado esperado:
- redirecionamento para `/login`
- acesso a rotas autenticadas volta a exigir sessao

## 11. Checklist final de aceite

Considere o piloto local validado quando todos os itens abaixo forem verdadeiros:

- login e logout funcionam
- Home carrega sem travar
- cada rota mostra seu proprio conteudo
- casos permitem alterar classificacao
- biometria pode ser enviada no detalhe do caso
- cameras podem ser habilitadas e desabilitadas
- worker fica `Ready` e processa frames
- incidentes podem ser visualizados e revisados
- auditoria reflete as alteracoes operacionais
- API, Web e Worker respondem com `Ready`
