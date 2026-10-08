// QA exclusivamente local: exemplos fictícios, contas aleatórias e armazenamento temporário.
// Requer build Debug, pacote playwright e Chrome local (ou PIERSEC_NAVEGADOR).
// Opcional: PIERSEC_QA_PREVIAS aponta para a pasta dos screenshots fictícios.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const net = require('node:net');
const { spawn } = require('node:child_process');
const raiz = path.resolve(__dirname, '..');
const endereco = 'https://localhost:5445';
const qa = fs.mkdtempSync(path.join(os.tmpdir(), 'piersec-executivo-'));
const senha = crypto.randomBytes(24).toString('base64');
const sal = crypto.randomBytes(16);
const hash = 'PBKDF2-SHA256$600000$' + sal.toString('base64') + '$' + crypto.pbkdf2Sync(senha,sal,600000,32,'sha256').toString('base64');
const usuarios = ['analista','executivo'].map(papel => ({Login:papel+'-teste',Papel:papel,HashSenha:hash,CriadoEmUtc:new Date().toISOString(),Segredo2faPendente:papel === 'executivo' ? crypto.randomBytes(64).toString('base64') : null}));
fs.writeFileSync(path.join(qa,'usuarios.json'),JSON.stringify(usuarios));
let servidor, navegador;
function estadoAnalises() {
    const resultado = {};
    for (const pasta of ['lotes','operacao']) {
        const percorrer = dir => {
            if (!fs.existsSync(dir)) return;
            for (const item of fs.readdirSync(dir,{withFileTypes:true})) {
                const arquivo = path.join(dir,item.name);
                if (item.isDirectory()) percorrer(arquivo);
                else resultado[path.relative(qa,arquivo)] = crypto.createHash('sha256').update(fs.readFileSync(arquivo)).digest('hex');
            }
        };
        percorrer(path.join(qa,pasta));
    }
    return resultado;
}
async function entrar(contexto, papel) {
    const pagina = await contexto.newPage();
    await pagina.goto(endereco+'/Entrar');
    await pagina.locator('#login').fill(papel+'-teste');
    await pagina.locator('#senha').fill(senha);
    await Promise.all([pagina.waitForURL(endereco+'/'),pagina.getByRole('button',{name:'Entrar',exact:true}).click()]);
    return pagina;
}
function codigo2fa(chave, passo = Math.floor(Date.now()/30000)) {
    const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
    const bits = [...chave.replaceAll(' ','')].map(c=>alfabeto.indexOf(c).toString(2).padStart(5,'0')).join('');
    const segredo = Buffer.from(bits.match(/.{8}/g).map(b=>parseInt(b,2)));
    const contador = Buffer.alloc(8); contador.writeBigUInt64BE(BigInt(passo));
    const hash = crypto.createHmac('sha1',segredo).update(contador).digest();
    const deslocamento = hash[hash.length-1] & 15;
    return ((hash.readUInt32BE(deslocamento) & 0x7fffffff) % 1000000).toString().padStart(6,'0');
}
async function semTransbordamento(pagina) {
    assert(await pagina.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Transbordamento horizontal');
}
(async () => {
    await new Promise((resolve,reject) => { const teste=net.createServer(); teste.once('error',reject); teste.listen(5445,'127.0.0.1',()=>teste.close(resolve)); });
    const dotnet = path.join(process.env.USERPROFILE,'.dotnet','dotnet.exe');
    servidor = spawn(dotnet,[path.join(raiz,'src/Piersec.Web/bin/Debug/net10.0/Piersec.Web.dll'),'--urls',endereco],{
        cwd:path.join(raiz,'src/Piersec.Web'),windowsHide:true,stdio:'ignore',
        env:{...process.env,DOTNET_ROOT:path.dirname(dotnet),Piersec__PastaDados:qa,Piersec__PastaRegras:path.join(raiz,'regras'),PIERSEC_ABRIR_NAVEGADOR:'0',PIERSEC_REDEFINIR_ADMIN:'0'}
    });
    servidor.once('error',erro=>{throw erro;});
    navegador = await chromium.launch({executablePath:process.env.PIERSEC_NAVEGADOR || 'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
    const contexto = await navegador.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:1050},colorScheme:'light'});
    // Nada externo é necessário ou permitido neste roteiro.
    await contexto.route('**/*',route=>new URL(route.request().url()).hostname === 'localhost' ? route.continue() : route.abort());
    for(let tentativa=0;tentativa<60;tentativa++) {
        try { if ((await contexto.request.get(endereco+'/Entrar')).ok()) break; } catch { }
        await new Promise(resolve=>setTimeout(resolve,250));
    }
    const pagina = await entrar(contexto,'analista');
    const erros = [];
    pagina.on('pageerror',erro=>erros.push(erro.message));
    async function enviar() {
        await pagina.goto(endereco+'/');
        await pagina.locator('#arquivos').setInputFiles(['vulnerabilidades-ficticio.csv','alertas-ficticio.csv'].map(nome=>path.join(raiz,'exemplos',nome)));
        await Promise.all([pagina.waitForURL(/\/Lote\/[^/?#]+$/,{timeout:90000}),pagina.locator('#botao-enviar').click()]);
        await pagina.locator('[data-painel-executivo]').waitFor();
        return new URL(pagina.url()).pathname;
    }
    await enviar();
    const lote = await enviar();
    assert.equal(await pagina.locator('.exec-historico a').count(),2);
    async function unica(id) {
        assert.equal(await pagina.locator('[data-etapa]:visible').count(),1,'Exibir só uma seção');
        assert(await pagina.locator('#'+id).isVisible(),'Seção esperada: '+id);
        assert.equal(await pagina.locator('[data-aba="'+id+'"]').getAttribute('aria-selected'),'true');
    }
    await unica('panorama');
    assert.equal(await pagina.locator('.exec-negocio > section:visible').count(),4);
    assert.match(await pagina.locator('[data-resumo-prejuizo]').innerText(),/R\$/);
    assert.match(await pagina.locator('[data-resumo-gasto]').innerText(),/A estimar/);
    assert.match(await pagina.locator('[data-resumo-causa]').innerText(),/Falhas nos programas/);
    assert.match(await pagina.locator('[data-resumo-solucao]').innerText(),/Atualizar/);
    assert.equal(await pagina.locator('.exec-mais-panorama').getAttribute('open'),null);
    assert(await pagina.locator('[data-etapa-anterior]').isDisabled());
    await pagina.locator('[data-etapa-proxima]').click();
    await unica('servicos');
    await pagina.locator('[data-etapa-proxima]').click();
    await unica('simulacao');
    await pagina.locator('[data-aba=fontes]').click();
    await unica('fontes');
    await pagina.goBack(); await unica('simulacao');
    await pagina.goForward(); await unica('fontes');
    await pagina.locator('[data-aba=simulacao]').click();
    await pagina.locator('[data-aba=panorama]').click();
    await pagina.locator('[data-resumo-causa] a').click();
    assert.equal(await pagina.locator('.exec-mais-panorama').getAttribute('open'),'');
    await pagina.locator('[data-resumo-prejuizo] a').click(); await unica('servicos');
    await pagina.locator('[data-aba=panorama]').click();
    await pagina.locator('[data-resumo-solucao] a').click(); await unica('simulacao');
    const checkbox = pagina.locator('[name=acoesSelecionadas]').first();
    const ordem = await checkbox.getAttribute('value');
    const baseline = await (await contexto.request.get(endereco+lote+'?handler=Simular')).json();
    const antes = estadoAnalises();
    await checkbox.check();
    await pagina.waitForFunction(() => document.querySelector('#resultado-simulacao').getAttribute('aria-busy')==='false');
    const simulado = await (await contexto.request.get(endereco+lote+'?handler=Simular&acoesSelecionadas='+ordem)).json();
    assert(simulado.score < baseline.score, 'Correção precisa reduzir o risco fictício');
    await pagina.locator('[data-aba=servicos]').click();
    await unica('servicos');
    await pagina.locator('[data-aba=simulacao]').click();
    assert(await checkbox.isChecked(),'Trocar de seção deve preservar a seleção');
    assert.equal(await pagina.locator('[data-score-simulado]').innerText(),Math.round(simulado.score).toLocaleString('pt-BR'));
    for(const entrada of ['999','invalido']) assert.equal((await contexto.request.get(endereco+lote+'?handler=Simular&acoesSelecionadas='+entrada)).status(),400);
    // Falha de rede não deixa o cenário anterior parecer atualizado.
    await pagina.route('**/*handler=Simular*',route=>route.abort());
    await checkbox.uncheck();
    await pagina.locator('[data-erro-simulacao]:not([hidden])').waitFor();
    assert.equal(await pagina.locator('[data-faixa-simulada]').innerText(),'Não atualizado');
    await pagina.unroute('**/*handler=Simular*');
    await pagina.locator('[data-limpar-simulacao]').click();
    await pagina.waitForFunction(() => document.querySelector('#resultado-simulacao').getAttribute('aria-busy')==='false');
    assert.equal(await pagina.locator('[data-score-simulado]').innerText(),Math.round(baseline.score).toLocaleString('pt-BR'));
    const menuConta = pagina.locator('.conta-menu');
    const abrirConta = pagina.locator('.conta-menu > summary');
    assert.equal(await menuConta.getAttribute('open'),null);
    await abrirConta.focus(); await pagina.keyboard.press('Enter');
    const tema = pagina.locator('#trocar-tema');
    assert(await tema.isVisible());
    assert.equal(await pagina.locator('.topo a[href="/DoisFatores"], .topo a[href="/TrocarSenha"], .topo #trocar-tema').count(),0);
    assert.equal(await pagina.locator('.conta-opcoes a').count(),2);
    assert.equal(await pagina.locator('.conta-opcoes form input[name="__RequestVerificationToken"]').count(),1);
    const barra = await pagina.locator('.lateral').boundingBox();
    const conta = await menuConta.boundingBox();
    const opcoes = await pagina.locator('.conta-opcoes').boundingBox();
    assert(conta.y > barra.y + barra.height / 2,'Conta deve ficar na parte inferior');
    assert(opcoes.y >= barra.y && opcoes.y + opcoes.height <= conta.y,'Menu abre acima do botão, dentro da barra');
    await pagina.keyboard.press('Escape');
    assert.equal(await menuConta.getAttribute('open'),null);
    assert(await abrirConta.evaluate(el=>document.activeElement===el));
    await abrirConta.click();
    await pagina.locator('.selo-confidencial').click();
    assert.equal(await menuConta.getAttribute('open'),null);
    await abrirConta.click();
    await tema.click();
    assert.equal(await pagina.locator('html').getAttribute('data-tema'),'escuro');
    await pagina.reload();
    assert.equal(await pagina.locator('html').getAttribute('data-tema'),'escuro');
    const previas = process.env.PIERSEC_QA_PREVIAS;
    if(previas) fs.mkdirSync(previas,{recursive:true});
    await abrirConta.click();
    if(previas) await pagina.screenshot({path:path.join(previas,'conta-menu-escuro-ficticio.png'),fullPage:true});
    await tema.click();
    if(previas) await pagina.screenshot({path:path.join(previas,'conta-menu-claro-ficticio.png'),fullPage:true});
    await pagina.keyboard.press('Escape');
    await pagina.locator('[data-aba=panorama]').click();
    if(await pagina.locator('.exec-mais-panorama').getAttribute('open') !== null) await pagina.locator('.exec-mais-panorama > summary').click();
    if(previas) await pagina.screenshot({path:path.join(previas,'executivo-claro-ficticio.png'),fullPage:true});
    await semTransbordamento(pagina);
    await pagina.locator('[data-apresentar]').click();
    assert.equal(await pagina.locator('body').getAttribute('data-apresentacao'),'true');
    assert.equal(await pagina.locator('.topo').isVisible(),false);
    for(const id of ['servicos','simulacao','fontes','visao-tecnica']) {
        await pagina.keyboard.press('ArrowRight');
        await unica(id);
    }
    assert(await pagina.locator('[data-etapa-proxima]').isDisabled());
    await pagina.keyboard.press('ArrowLeft'); await unica('fontes');
    await pagina.keyboard.press('Escape');
    assert.equal(await pagina.locator('body').getAttribute('data-apresentacao'),null);
    await pagina.goto(endereco+lote+'#achados');
    assert(await pagina.locator('#visao-tecnica').getAttribute('open') !== null);
    await pagina.setViewportSize({width:390,height:844});
    await pagina.goto(endereco+lote);
    await semTransbordamento(pagina);
    if (previas) await pagina.screenshot({path:path.join(previas,'executivo-celular-ficticio.png'),fullPage:true});
    for(const id of ['servicos','simulacao','fontes','visao-tecnica']) {
        await pagina.locator('[data-aba="'+id+'"]').click();
        await unica(id); await semTransbordamento(pagina);
        if(previas) await pagina.screenshot({path:path.join(previas,'executivo-'+id+'-celular-ficticio.png'),fullPage:true});
    }
    await pagina.locator('[data-aba=fontes]').focus();
    await pagina.keyboard.press('Home'); await unica('panorama');
    await pagina.keyboard.press('End'); await unica('visao-tecnica');
    await abrirConta.click(); await semTransbordamento(pagina);
    if(previas) await pagina.screenshot({path:path.join(previas,'conta-menu-celular-ficticio.png'),fullPage:true});
    await pagina.keyboard.press('Escape');
    // Mesmo formulário GET funciona sem JavaScript, sem gravar nova análise.
    const semJs = await navegador.newContext({ignoreHTTPSErrors:true,javaScriptEnabled:false});
    await semJs.addCookies(await contexto.cookies());
    const fallback = await semJs.newPage();
    await fallback.goto(endereco+lote);
    assert(await fallback.locator('#panorama').isVisible());
    await fallback.locator('.conta-menu > summary').click();
    assert(await fallback.locator('.conta-opcoes a[href="/DoisFatores"]').isVisible());
    assert.equal(await fallback.locator('#simulacao').isVisible(),false);
    await fallback.locator('[data-aba=servicos]').click();
    assert(await fallback.locator('#servicos').isVisible());
    await fallback.locator('[data-aba=simulacao]').click();
    assert(await fallback.locator('#simulacao').isVisible());
    await fallback.locator('[name=acoesSelecionadas]').first().check();
    await Promise.all([fallback.waitForURL(/simular=true/),fallback.getByRole('button',{name:'Recalcular cenário',exact:true}).click()]);
    assert.equal(await fallback.locator('[data-score-simulado]').innerText(),Math.round(simulado.score).toLocaleString('pt-BR'));
    await fallback.goto(endereco+lote+'?simular=true&acoesSelecionadas=invalido');
    assert(await fallback.locator('[data-erro-simulacao]').isVisible());
    assert.deepEqual(estadoAnalises(),antes,'Simular não pode gravar lote, relatório ou operação');
    const executivo = await navegador.newContext({ignoreHTTPSErrors:true});
    const diretoria = await entrar(executivo,'executivo');
    await diretoria.goto(endereco+lote);
    assert.equal(await diretoria.locator('#visao-tecnica').count(),0);
    assert.equal(await diretoria.locator('a[href*="/Eventos"]').count(),0);
    assert(await diretoria.locator('[data-painel-executivo]').isVisible());
    assert.equal(await diretoria.locator('.lateral-rodape a').count(),2);
    await diretoria.locator('.conta-menu > summary').click();
    await diretoria.locator('.lateral-rodape a[href="/DoisFatores"]').click();
    assert.equal(new URL(diretoria.url()).pathname,'/DoisFatores');
    assert.equal(await diretoria.locator('.lateral-rodape a[href="/DoisFatores"]').getAttribute('aria-current'),'page');
    // Reproduz chave pendente de outra instalação: precisa mostrar cadastro real, não /Erro.
    assert(await diretoria.getByRole('heading',{name:'Verificação em duas etapas',exact:true}).isVisible());
    assert(await diretoria.getByRole('status').filter({hasText:'Geramos um novo QR code'}).isVisible());
    assert.equal(await diretoria.getByRole('heading',{name:'Algo deu errado',exact:true}).count(),0);
    const chaveExecutivo = await diretoria.locator('.chave-2fa').innerText();
    await diretoria.reload();
    assert((await diretoria.locator('.chave-2fa').innerText()) === chaveExecutivo,'Recarregar preserva a chave pendente nova');
    assert.equal(await diretoria.getByRole('status').filter({hasText:'Geramos um novo QR code'}).count(),0);
    await diretoria.locator('#codigo').fill(codigo2fa(chaveExecutivo));
    await Promise.all([diretoria.waitForURL(/handler=Confirmar/),diretoria.getByRole('button',{name:'Ativar',exact:true}).click()]);
    assert.equal(await diretoria.locator('.codigos-recuperacao li').count(),8);
    await diretoria.goto(endereco+'/DoisFatores');
    assert(await diretoria.locator('form[action*="handler=NovosCodigos"]').isVisible());
    assert.equal(await diretoria.locator('.chave-2fa').count(),0);
    console.log('OK: executivo recupera cadastro 2FA ilegível, preserva novo QR no reload e ativa com TOTP.');
    await diretoria.locator('.conta-menu > summary').click();
    await diretoria.locator('.lateral-rodape a[href="/TrocarSenha"]').click();
    assert.equal(new URL(diretoria.url()).pathname,'/TrocarSenha');
    assert.equal(await diretoria.locator('.lateral-rodape a[href="/TrocarSenha"]').getAttribute('aria-current'),'page');
    await diretoria.goto(endereco+lote);
    assert.equal(await diretoria.locator('[data-aba]').count(),4);
    await diretoria.locator('[data-aba=simulacao]').click();
    await diretoria.locator('[name=acoesSelecionadas]').first().check();
    await diretoria.waitForFunction(() => document.querySelector('#resultado-simulacao').getAttribute('aria-busy')==='false');
    await diretoria.goto(endereco+lote+'/Eventos');
    assert(new URL(diretoria.url()).pathname.startsWith('/Negado'));
    await diretoria.locator('.conta-menu > summary').click();
    await Promise.all([diretoria.waitForURL(/\/Entrar/),diretoria.locator('.conta-opcoes button[type=submit]').click()]);
    assert.equal(await diretoria.locator('.conta-menu').count(),0);
    await pagina.goto(endereco+'/');
    await pagina.locator('#arquivos').setInputFiles({name:'sem-dados-ficticio.csv',mimeType:'text/csv',buffer:Buffer.from('coluna\nx')});
    await Promise.all([pagina.waitForURL(/\/Lote\/[^/?#]+$/,{timeout:90000}),pagina.locator('#botao-enviar').click()]);
    await unica('panorama');
    assert.match(await pagina.locator('[data-resumo-causa]').innerText(),/Dados insuficientes/);
    assert.match(await pagina.locator('[data-resumo-gasto]').innerText(),/A estimar/);
    assert.deepEqual(erros,[], 'Erros JavaScript no navegador');
    console.log('OK: envio fictício, histórico, simulador HTTP/JS/sem JS, rejeição inválida e nenhuma gravação.');
    console.log('OK: seção única, títulos/setas/teclado, histórico do navegador, seleção preservada, navegação sem JS, temas, apresentação, evidências e 390 px.');
})().catch(erro=>{console.error(erro);process.exitCode=1;}).finally(async()=>{
    if(navegador) await navegador.close();
    if(servidor) servidor.kill();
    // Só remove a pasta temporária criada por este processo; nunca usa o var real.
    if(path.dirname(qa)===os.tmpdir() && path.basename(qa).startsWith('piersec-executivo-')) fs.rmSync(qa,{recursive:true,force:true});
});
