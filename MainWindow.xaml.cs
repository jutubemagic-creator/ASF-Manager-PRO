using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;

namespace ASFManagerPRO
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Account> Accounts { get; set; } = new();
        private string dataPath;
        private string appDataFolder;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Closing += Window_Closing;
            this.PreviewKeyDown += Window_PreviewKeyDown;
            
            // Данные сохраняются в LOCALAPPDATA (постоянное место)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            appDataFolder = Path.Combine(localAppData, "ASF_Manager_PRO");
            
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);
            
            dataPath = Path.Combine(appDataFolder, "accounts.json");
            
            LoadAccounts();
            
            Accounts.CollectionChanged += (s, e) => { SaveAccounts(); };
            
            InitializeWebView();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
                SendToJS("hotkey", "new");
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                SendToJS("hotkey", "save");
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                SendToJS("hotkey", "search");
            else if (e.Key == Key.Delete)
                SendToJS("hotkey", "delete");
        }

        private async void InitializeWebView()
        {
            try
            {
                string webViewDataPath = Path.Combine(appDataFolder, "WebView2Data");
                if (!Directory.Exists(webViewDataPath))
                    Directory.CreateDirectory(webViewDataPath);
                
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, webViewDataPath);
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                // ВСТРОЕННЫЙ HTML (всё в одном файле)
                string html = GetEmbeddedHtml();
                webView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nУстановите WebView2 Runtime:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703", 
                    "ASF Manager PRO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetEmbeddedHtml()
        {
            return @"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <title>ASF Manager PRO v3.3</title>
    <script src='https://cdn.tailwindcss.com'></script>
    <link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.6.0/css/all.min.css'>
    <style>
        * { font-family: system-ui, sans-serif; }
        body { background: #0a0a0f; color: white; }
        .glass { background: rgba(255,255,255,0.05); backdrop-filter: blur(12px); border: 1px solid rgba(255,255,255,0.1); }
        .card { transition: all 0.3s; cursor: pointer; background: linear-gradient(135deg, rgba(255,255,255,0.08) 0%, rgba(255,255,255,0.03) 100%); border: 1px solid rgba(255,255,255,0.08); }
        .card:hover { transform: translateY(-4px); border-color: rgba(0,180,255,0.3); }
        .tab-active { background: #00b4ff; color: black; }
        .toast { position: fixed; bottom: 20px; right: 20px; background: #00b4ff; color: black; padding: 12px 24px; border-radius: 30px; z-index: 1000; }
        .btn-primary { background: linear-gradient(135deg, #00b4ff 0%, #0099cc 100%); }
        .btn-primary:hover { transform: translateY(-1px); }
        ::-webkit-scrollbar { width: 8px; }
        ::-webkit-scrollbar-track { background: #1f2937; }
        ::-webkit-scrollbar-thumb { background: #00b4ff; border-radius: 4px; }
    </style>
</head>
<body>

<div class='flex h-screen'>
    <div class='w-80 bg-[#0f1117] border-r border-gray-800 p-6 flex flex-col'>
        <div class='flex items-center gap-3 mb-8'>
            <div class='w-12 h-12 bg-gradient-to-br from-[#00b4ff] to-cyan-400 rounded-2xl flex items-center justify-center text-3xl'>🎮</div>
            <div><h1 class='text-2xl font-bold'>ASF PRO</h1><p class='text-xs text-gray-500'>Manager v3.3</p></div>
        </div>
        <button onclick='addNewAccount()' class='btn-primary w-full py-3 rounded-xl font-semibold flex items-center justify-center gap-2 mb-3'><i class='fas fa-plus'></i> Добавить аккаунт</button>
        <button onclick='runASFForAll()' class='w-full py-3 bg-green-600 hover:bg-green-500 rounded-xl font-semibold flex items-center justify-center gap-2 mb-6'><i class='fas fa-play'></i> Запустить ASF для всех</button>
        <div class='space-y-1'>
            <button onclick='switchTab(`accounts`)' id='tabAccounts' class='tab-btn w-full text-left px-4 py-3 rounded-xl flex items-center gap-3 hover:bg-white/10'><i class='fas fa-users w-5'></i> Аккаунты</button>
            <button onclick='switchTab(`inventory`)' id='tabInventory' class='tab-btn w-full text-left px-4 py-3 rounded-xl flex items-center gap-3 hover:bg-white/10'><i class='fas fa-box w-5'></i> Инвентарь</button>
            <button onclick='switchTab(`stats`)' id='tabStats' class='tab-btn w-full text-left px-4 py-3 rounded-xl flex items-center gap-3 hover:bg-white/10'><i class='fas fa-chart-line w-5'></i> Статистика</button>
            <button onclick='switchTab(`settings`)' id='tabSettings' class='tab-btn w-full text-left px-4 py-3 rounded-xl flex items-center gap-3 hover:bg-white/10'><i class='fas fa-cog w-5'></i> Настройки</button>
        </div>
        <div class='mt-auto pt-6 text-xs text-gray-500 border-t border-gray-800'><i class='fas fa-database'></i> <span id='accountCount'>0 аккаунтов</span></div>
    </div>
    <div class='flex-1 p-6 overflow-auto' id='mainContent'></div>
</div>

<!-- Модальные окна -->
<div id='detailModal' class='hidden fixed inset-0 bg-black/80 flex items-center justify-center z-50 p-4'>
    <div class='glass w-[950px] rounded-2xl p-6 max-h-[85vh] overflow-auto'>
        <div class='flex justify-between items-center mb-5'><h2 id='detailTitle' class='text-2xl font-bold'></h2><button onclick='closeDetailModal()' class='text-gray-400 hover:text-white text-3xl'>&times;</button></div>
        <div id='detailContent' class='mb-5'></div>
        <div class='flex gap-3'>
            <button onclick='editFromDetail()' class='flex-1 py-3 bg-[#00b4ff] text-black font-semibold rounded-xl'><i class='fas fa-edit'></i> Редактировать</button>
            <button onclick='runASFFromDetail()' class='flex-1 py-3 bg-green-600 font-semibold rounded-xl'><i class='fas fa-play'></i> Запустить ASF</button>
            <button onclick='deleteFromDetail()' class='flex-1 py-3 bg-red-600 font-semibold rounded-xl'><i class='fas fa-trash'></i> Удалить</button>
            <button onclick='closeDetailModal()' class='flex-1 py-3 bg-gray-700 font-semibold rounded-xl'>Закрыть</button>
        </div>
    </div>
</div>

<div id='editModal' class='hidden fixed inset-0 bg-black/80 flex items-center justify-center z-50 p-4'>
    <div class='glass w-[850px] rounded-2xl p-6 max-h-[85vh] overflow-auto'>
        <h2 id='editTitle' class='text-2xl font-bold mb-5'>Редактирование</h2>
        <div class='grid grid-cols-2 gap-4'>
            <div><label class='text-gray-400 text-sm'>Логин Steam *</label><input id='editLogin' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>Пароль Steam</label><input id='editPassword' type='password' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>Steam ID</label><input id='editSteamId' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>Баланс</label><input id='editBalance' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5' value='0 ₽'></div>
            <div><label class='text-gray-400 text-sm'>Почта</label><input id='editEmail' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>Пароль почты</label><input id='editEmailPass' type='password' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>Прокси</label><input id='editProxy' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div><label class='text-gray-400 text-sm'>PIN-код</label><input id='editPin' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></div>
            <div class='col-span-2'><label class='text-gray-400 text-sm'>MaFile</label><textarea id='editMaFile' rows='3' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5 font-mono text-xs'></textarea></div>
            <div class='col-span-2'><label class='text-gray-400 text-sm'>Заметки</label><textarea id='editNotes' rows='2' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5'></textarea></div>
        </div>
        <div class='flex gap-3 mt-6'>
            <button onclick='saveEditedAccount()' class='flex-1 py-3 bg-[#00b4ff] text-black font-semibold rounded-xl'>Сохранить</button>
            <button onclick='closeEditModal()' class='flex-1 py-3 bg-gray-700 font-semibold rounded-xl'>Отмена</button>
        </div>
    </div>
</div>

<div id='massEditModal' class='hidden fixed inset-0 bg-black/80 flex items-center justify-center z-50 p-4'>
    <div class='glass w-[500px] rounded-2xl p-6'>
        <h2 class='text-2xl font-bold mb-5'>Массовое редактирование</h2>
        <select id='massEditField' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5 mb-4'>
            <option value='Proxy'>Прокси</option><option value='Notes'>Заметки</option><option value='Status'>Статус</option>
        </select>
        <input type='text' id='massEditValue' class='w-full bg-[#1f2937] rounded-xl px-4 py-2.5 mb-6' placeholder='Новое значение'>
        <div class='flex gap-3'><button onclick='applyMassEdit()' class='flex-1 py-3 bg-[#00b4ff] text-black font-semibold rounded-xl'>Применить</button><button onclick='closeMassEditModal()' class='flex-1 py-3 bg-gray-700 font-semibold rounded-xl'>Отмена</button></div>
    </div>
</div>

<script>
let accounts = [];
let currentTab = 'accounts';
let currentDetailAccount = null;
let currentEditId = null;
let currentFilter = '';
let currentSort = 'date-desc';
let showPasswords = {};
let selectedAccountsForMass = new Set();

const games = [
    { name: 'Counter-Strike 2', appId: '730' }, { name: 'Dota 2', appId: '570' }, { name: 'Team Fortress 2', appId: '440' },
    { name: 'Rust', appId: '252490' }, { name: 'GTA V', appId: '271590' }, { name: 'PUBG', appId: '578080' }
];

function switchTab(tab) {
    currentTab = tab;
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('tab-active'));
    document.getElementById('tab' + tab.charAt(0).toUpperCase() + tab.slice(1)).classList.add('tab-active');
    if (tab === 'accounts') renderAccounts();
    else if (tab === 'inventory') renderInventory();
    else if (tab === 'stats') renderStats();
    else if (tab === 'settings') renderSettings();
}

function renderAccounts() {
    let filtered = [...accounts];
    if (currentFilter) {
        let search = currentFilter.toLowerCase();
        filtered = filtered.filter(acc => acc.Login.toLowerCase().includes(search) || (acc.Email && acc.Email.toLowerCase().includes(search)));
    }
    if (currentSort === 'date-desc') filtered.sort((a,b) => new Date(b.CreatedAt) - new Date(a.CreatedAt));
    else if (currentSort === 'date-asc') filtered.sort((a,b) => new Date(a.CreatedAt) - new Date(b.CreatedAt));
    else if (currentSort === 'login-asc') filtered.sort((a,b) => a.Login.localeCompare(b.Login));
    else if (currentSort === 'login-desc') filtered.sort((a,b) => b.Login.localeCompare(a.Login));
    
    let html = `<div class='mb-6 flex gap-4 flex-wrap'>
        <input type='text' id='searchInput' placeholder='Поиск...' class='flex-1 bg-[#1f2937] rounded-xl px-4 py-2.5' onkeyup='updateSearch()'>
        <select id='sortSelect' onchange='updateSort()' class='bg-[#1f2937] rounded-xl px-4 py-2.5'>
            <option value='date-desc'>Новые</option><option value='date-asc'>Старые</option>
            <option value='login-asc'>А-Я</option><option value='login-desc'>Я-А</option>
        </select>
        <button onclick='showMassEditModal()' class='px-4 py-2.5 bg-[#374151] hover:bg-[#00b4ff] rounded-xl'><i class='fas fa-check-double'></i> Массовое ({selectedAccountsForMass.size})</button>
        <button onclick='deleteAllAccounts()' class='px-4 py-2.5 bg-red-600 rounded-xl'><i class='fas fa-trash'></i> Удалить все</button>
    </div><div class='grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5'>`;
    
    filtered.forEach(acc => {
        let avatarColor = 'hsl(' + (Math.abs(acc.Login.split('').reduce((a,b)=>a+b.charCodeAt(0),0) % 360) + ', 70%, 55%)';
        html += `<div class='card rounded-2xl p-5 relative' data-id='${acc.Id}' onclick='showAccountDetail(\"${acc.Id}\")'>
            <div class='absolute top-3 left-3' onclick='event.stopPropagation()'><input type='checkbox' class='accent-[#00b4ff]' onchange='toggleMassSelect(\"${acc.Id}\")' ${selectedAccountsForMass.has(acc.Id) ? 'checked' : ''}></div>
            <div class='flex justify-between items-start ml-7'><div><h3 class='text-xl font-semibold'>${escapeHtml(acc.Login)}</h3><p class='text-gray-400 text-xs'>${acc.Email || 'нет почты'}</p></div>
            <span class='px-3 py-1 text-xs rounded-full ${acc.Status === 'Online' ? 'bg-green-500' : 'bg-gray-600'}'>${acc.Status === 'Online' ? 'Онлайн' : 'Оффлайн'}</span></div>
            <div class='mt-4 grid grid-cols-2 gap-2 text-xs'><div>Прокси: ${acc.Proxy ? '✅' : '❌'}</div><div>MaFile: ${acc.MaFile ? '✅' : '❌'}</div><div>Баланс: <span class='text-emerald-400'>${acc.Balance}</span></div><div>Вход: ${acc.LastLogin ? new Date(acc.LastLogin).toLocaleDateString() : 'нет'}</div></div>
            <div class='mt-4 flex gap-2' onclick='event.stopPropagation()'>
                <button onclick='runASF(\"${acc.Login}\")' class='flex-1 py-2 bg-green-600 rounded-lg'><i class='fas fa-play'></i> ASF</button>
                <button onclick='viewInventory(\"${acc.SteamId || acc.Login}\")' class='flex-1 py-2 bg-purple-600 rounded-lg'><i class='fas fa-box'></i> Инвентарь</button>
                <button onclick='editAccount(\"${acc.Id}\")' class='px-3 py-2 bg-gray-700 rounded-lg'><i class='fas fa-edit'></i></button>
                <button onclick='deleteAccount(\"${acc.Id}\")' class='px-3 py-2 bg-red-600 rounded-lg'><i class='fas fa-trash'></i></button>
            </div>
        </div>`;
    });
    html += filtered.length === 0 ? '<div class="col-span-full text-center py-20 text-gray-500">Нет аккаунтов</div>' : '';
    document.getElementById('mainContent').innerHTML = html;
    document.getElementById('accountCount').innerText = accounts.length + ' аккаунтов';
    updateMassSelectUI();
}

function renderStats() {
    let online = accounts.filter(a => a.Status === 'Online').length;
    let balance = accounts.reduce((s, a) => s + (parseFloat(a.Balance.replace(/[^0-9.-]/g, '')) || 0), 0);
    document.getElementById('mainContent').innerHTML = `<div class='max-w-4xl mx-auto text-center'><h2 class='text-3xl font-bold mb-6'>Статистика</h2>
    <div class='grid grid-cols-2 gap-4'><div class='glass p-6 rounded-2xl'><div class='text-4xl font-bold'>${accounts.length}</div><div>Аккаунтов</div></div>
    <div class='glass p-6 rounded-2xl'><div class='text-4xl font-bold text-green-400'>${online}</div><div>Онлайн</div></div>
    <div class='glass p-6 rounded-2xl'><div class='text-4xl font-bold text-emerald-400'>${balance.toFixed(2)} ₽</div><div>Баланс</div></div>
    <div class='glass p-6 rounded-2xl'><div class='text-4xl font-bold'>${accounts.filter(a => a.Proxy).length}</div><div>С прокси</div></div></div></div>`;
}

function renderInventory() {
    let gameOptions = games.map(g => `<option value='${g.appId}'>${g.name}</option>`).join('');
    document.getElementById('mainContent').innerHTML = `<div class='max-w-4xl mx-auto'><div class='glass p-6 rounded-2xl mb-6'>
    <input type='text' id='inventorySteamId' placeholder='Steam ID' class='w-full bg-[#1f2937] rounded-xl px-4 py-3 mb-3'>
    <select id='inventoryAppId' class='w-full bg-[#1f2937] rounded-xl px-4 py-3 mb-3'>${gameOptions}</select>
    <button onclick='loadInventory()' class='w-full py-3 bg-[#00b4ff] text-black rounded-xl font-semibold'>Загрузить</button></div>
    <div id='inventoryResult' class='glass p-6 rounded-2xl text-center text-gray-400'>Введите Steam ID</div></div>`;
}

function loadInventory() {
    let steamId = document.getElementById('inventorySteamId').value;
    let appId = document.getElementById('inventoryAppId').value;
    if (!steamId) { showToast('Введите Steam ID'); return; }
    document.getElementById('inventoryResult').innerHTML = '<div class="text-center py-10"><i class="fas fa-spinner fa-spin text-2xl"></i><p>Загрузка...</p></div>';
    window.chrome.webview.postMessage(JSON.stringify({ action: 'getInventory', data: steamId + '|' + appId }));
}

function viewInventory(steamId) { if (!steamId) { showToast('Укажите Steam ID'); return; } switchTab('inventory'); setTimeout(() => { let inp = document.getElementById('inventorySteamId'); if(inp) inp.value = steamId; loadInventory(); }, 100); }
function renderSettings() { document.getElementById('mainContent').innerHTML = `<div class='max-w-2xl mx-auto'><div class='glass p-6 rounded-2xl mb-6'><button onclick='exportData()' class='w-full mb-3 py-3 bg-green-600 rounded-xl'>Экспорт JSON</button><button onclick='importData()' class='w-full py-3 bg-blue-600 rounded-xl'>Импорт JSON</button></div><div class='glass p-6 rounded-2xl'><h2 class='text-2xl font-bold mb-2'>ASF Manager PRO v3.3</h2><p>Данные хранятся в: %LOCALAPPDATA%\\ASF_Manager_PRO</p></div></div>`; }
function showAccountDetail(id) { let acc = accounts.find(a => a.Id === id); if (!acc) return; currentDetailAccount = acc; let pass = showPasswords[acc.Id] ? acc.Password : '••••••••'; let html = `<div class='detail-row'><div class='detail-label'>Логин:</div><div>${escapeHtml(acc.Login)} <span class='copy-btn' onclick='copyToClipboard(\"${escapeHtml(acc.Login)}\")'>Копировать</span></div></div>` + `<div class='detail-row'><div class='detail-label'>Пароль:</div><div>${escapeHtml(pass)} <span class='view-btn' onclick='togglePassword(\"${acc.Id}\")'>${showPasswords[acc.Id] ? 'Скрыть' : 'Показать'}</span> <span class='copy-btn' onclick='copyToClipboard(\"${escapeHtml(acc.Password)}\")'>Копировать</span></div></div>`; document.getElementById('detailContent').innerHTML = html; document.getElementById('detailTitle').innerHTML = `<i class='fas fa-user-circle'></i> ${escapeHtml(acc.Login)}`; document.getElementById('detailModal').classList.remove('hidden'); }
function togglePassword(id) { showPasswords[id] = !showPasswords[id]; showAccountDetail(id); }
function deleteFromDetail() { if (currentDetailAccount && confirm('Удалить?')) { deleteAccount(currentDetailAccount.Id); closeDetailModal(); } }
function editFromDetail() { if (currentDetailAccount) { closeDetailModal(); editAccount(currentDetailAccount.Id); } }
function runASFFromDetail() { if (currentDetailAccount) { runASF(currentDetailAccount.Login); showToast('Запуск ASF...'); } }
function deleteAllAccounts() { if (confirm('Удалить все аккаунты?')) { window.chrome.webview.postMessage(JSON.stringify({ action: 'deleteAllAccounts' })); accounts = []; renderAccounts(); showToast('Все удалены'); } }
function runASFForAll() { if (accounts.length === 0) { showToast('Нет аккаунтов'); return; } if (confirm('Запустить ASF для всех?')) window.chrome.webview.postMessage(JSON.stringify({ action: 'runASFForAll' })); }
function exportData() { let data = JSON.stringify(accounts, null, 2); let blob = new Blob([data], {type: 'application/json'}); let url = URL.createObjectURL(blob); let a = document.createElement('a'); a.href = url; a.download = 'asf_backup.json'; a.click(); URL.revokeObjectURL(url); showToast('Экспорт выполнен'); }
function importData() { let input = document.createElement('input'); input.type = 'file'; input.accept = '.json'; input.onchange = e => { let file = e.target.files[0]; let reader = new FileReader(); reader.onload = ev => { try { let imported = JSON.parse(ev.target.result); if (Array.isArray(imported)) { accounts = imported; window.chrome.webview.postMessage(JSON.stringify({ action: 'saveAccounts', data: accounts })); showToast('Импортировано ' + accounts.length + ' аккаунтов'); renderAccounts(); } } catch { showToast('Ошибка импорта'); } }; reader.readAsText(file); }; input.click(); }
function addNewAccount() { currentEditId = null; document.getElementById('editTitle').innerText = 'Новый аккаунт'; clearEditForm(); document.getElementById('editModal').classList.remove('hidden'); }
function editAccount(id) { let acc = accounts.find(a => a.Id === id); if (!acc) return; currentEditId = id; document.getElementById('editTitle').innerText = 'Редактирование: ' + acc.Login; document.getElementById('editLogin').value = acc.Login || ''; document.getElementById('editPassword').value = acc.Password || ''; document.getElementById('editSteamId').value = acc.SteamId || ''; document.getElementById('editBalance').value = acc.Balance || '0 ₽'; document.getElementById('editEmail').value = acc.Email || ''; document.getElementById('editEmailPass').value = acc.EmailPass || ''; document.getElementById('editProxy').value = acc.Proxy || ''; document.getElementById('editPin').value = acc.Pin || ''; document.getElementById('editMaFile').value = acc.MaFile || ''; document.getElementById('editNotes').value = acc.Notes || ''; document.getElementById('editModal').classList.remove('hidden'); }
function clearEditForm() { document.getElementById('editLogin').value = 'acc_' + Date.now().toString().slice(-6); document.getElementById('editPassword').value = ''; document.getElementById('editSteamId').value = ''; document.getElementById('editBalance').value = '0 ₽'; document.getElementById('editEmail').value = ''; document.getElementById('editEmailPass').value = ''; document.getElementById('editProxy').value = ''; document.getElementById('editPin').value = ''; document.getElementById('editMaFile').value = ''; document.getElementById('editNotes').value = ''; }
function saveEditedAccount() { let data = { Id: currentEditId || 'acc-' + Date.now(), Login: document.getElementById('editLogin').value.trim(), Password: document.getElementById('editPassword').value, SteamId: document.getElementById('editSteamId').value, Email: document.getElementById('editEmail').value, EmailPass: document.getElementById('editEmailPass').value, Proxy: document.getElementById('editProxy').value, Pin: document.getElementById('editPin').value, MaFile: document.getElementById('editMaFile').value, Notes: document.getElementById('editNotes').value, Status: currentEditId ? (accounts.find(a => a.Id === currentEditId)?.Status || 'Offline') : 'Offline', Balance: document.getElementById('editBalance').value || '0 ₽', CreatedAt: currentEditId ? (accounts.find(a => a.Id === currentEditId)?.CreatedAt || new Date().toISOString()) : new Date().toISOString(), LastLogin: currentEditId ? (accounts.find(a => a.Id === currentEditId)?.LastLogin || '') : '', CardsRemaining: 0, GamesCount: 0 }; if (!data.Login) { showToast('Логин обязателен'); return; } if (currentEditId) { let idx = accounts.findIndex(a => a.Id === currentEditId); if (idx > -1) accounts[idx] = data; } else { accounts.unshift(data); } window.chrome.webview.postMessage(JSON.stringify({ action: 'saveAccounts', data: accounts })); closeEditModal(); renderAccounts(); showToast('Сохранено'); }
function deleteAccount(id) { if (confirm('Удалить аккаунт?')) { window.chrome.webview.postMessage(JSON.stringify({ action: 'deleteAccount', data: id })); accounts = accounts.filter(a => a.Id !== id); selectedAccountsForMass.delete(id); renderAccounts(); showToast('Удалён'); } }
function runASF(login) { window.chrome.webview.postMessage(JSON.stringify({ action: 'runASF', data: login })); }
function copyToClipboard(text) { if (!text) { showToast('Нет данных'); return; } window.chrome.webview.postMessage(JSON.stringify({ action: 'copyToClipboard', data: text })); showToast('Скопировано'); }
function showToast(msg) { let toast = document.createElement('div'); toast.className = 'toast'; toast.innerHTML = '<i class="fas fa-check-circle"></i> ' + msg; document.body.appendChild(toast); setTimeout(() => toast.remove(), 2500); }
function closeDetailModal() { document.getElementById('detailModal').classList.add('hidden'); currentDetailAccount = null; }
function closeEditModal() { document.getElementById('editModal').classList.add('hidden'); currentEditId = null; }
function escapeHtml(str) { if (!str) return ''; return str.replace(/[&<>]/g, function(m) { if (m === '&') return '&amp;'; if (m === '<') return '&lt;'; if (m === '>') return '&gt;'; return m; }); }
function updateSearch() { currentFilter = document.getElementById('searchInput')?.value || ''; renderAccounts(); }
function updateSort() { currentSort = document.getElementById('sortSelect')?.value || 'date-desc'; renderAccounts(); }
function toggleMassSelect(id) { if (selectedAccountsForMass.has(id)) selectedAccountsForMass.delete(id); else selectedAccountsForMass.add(id); updateMassSelectUI(); renderAccounts(); }
function updateMassSelectUI() { let btn = document.getElementById('massSelectBtn'); if (btn) btn.innerHTML = '<i class="fas fa-check-double"></i> Массовое (' + selectedAccountsForMass.size + ')'; }
function showMassEditModal() { if (selectedAccountsForMass.size === 0) { showToast('Выберите аккаунты'); return; } document.getElementById('massEditModal').classList.remove('hidden'); }
function closeMassEditModal() { document.getElementById('massEditModal').classList.add('hidden'); document.getElementById('massEditValue').value = ''; }
function applyMassEdit() { let field = document.getElementById('massEditField').value; let value = document.getElementById('massEditValue').value; if (!value) { showToast('Введите значение'); return; } let ids = Array.from(selectedAccountsForMass); let updateData = { AccountIds: ids, Fields: {} }; updateData.Fields[field] = value; window.chrome.webview.postMessage(JSON.stringify({ action: 'massUpdate', data: JSON.stringify(updateData) })); closeMassEditModal(); selectedAccountsForMass.clear(); showToast('Обновлено ' + ids.length + ' аккаунтов'); }
function receiveHotkey(action) { if (action === 'new') addNewAccount(); else if (action === 'save') { let btn = document.querySelector('#editModal:not(.hidden) button:first-child'); if (btn) btn.click(); } else if (action === 'search') document.getElementById('searchInput')?.focus(); else if (action === 'delete' && currentDetailAccount) deleteFromDetail(); }
window.receiveFromCSharp = function(msg) { if (msg.type === 'accounts') { accounts = msg.data || []; if (currentTab === 'accounts') renderAccounts(); if (currentTab === 'stats') renderStats(); document.getElementById('accountCount').innerText = accounts.length + ' аккаунтов'; } else if (msg.type === 'inventoryData') { let inv = msg.data; let items = inv.data?.assets || []; let desc = inv.data?.descriptions || []; let html = '<div class="grid grid-cols-2 md:grid-cols-4 gap-4">'; items.forEach(item => { let d = desc.find(x => x.classid === item.classid); if (d) html += `<div class="inventory-item rounded-xl p-3 text-center"><img src="https://steamcommunity.com/economy/image/${d.icon_url}" class="w-20 h-20 mx-auto mb-2"><p class="text-xs">${escapeHtml(d.name.substring(0, 30))}</p><p class="text-xs text-emerald-400">x${item.amount}</p></div>`; }); html += '</div>'; document.getElementById('inventoryResult').innerHTML = html; } else if (msg.type === 'inventoryError') { document.getElementById('inventoryResult').innerHTML = '<div class="text-center text-red-400 py-10">' + msg.data + '</div>'; } else if (msg.type === 'asfStarted') { showToast(msg.data); if (currentTab === 'accounts') renderAccounts(); } else if (msg.type === 'asfError') { showToast(msg.data); } else if (msg.type === 'hotkey') { receiveHotkey(msg.data); } else if (msg.type === 'massUpdateComplete') { renderAccounts(); } };
window.onload = () => { window.chrome.webview.postMessage(JSON.stringify({ action: 'getAccounts' })); switchTab('accounts'); };
</script>
</body>
</html>";
        }

        private async void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JsonSerializer.Deserialize<WebMessage>(json);

                switch (msg?.Action)
                {
                    case "saveAccounts":
                        if (msg.Data != null)
                        {
                            var newAccounts = JsonSerializer.Deserialize<ObservableCollection<Account>>(msg.Data);
                            if (newAccounts != null)
                            {
                                Accounts.Clear();
                                foreach (var acc in newAccounts)
                                    Accounts.Add(acc);
                            }
                            SaveAccounts();
                            SendToJS("accounts", Accounts);
                        }
                        break;

                    case "getAccounts":
                        SendToJS("accounts", Accounts);
                        break;

                    case "getInventory":
                        await GetInventory(msg.Data);
                        break;

                    case "runASF":
                        RunASF(msg.Data);
                        break;

                    case "runASFForAll":
                        RunASFForAll();
                        break;

                    case "updateLastLogin":
                        UpdateLastLogin(msg.Data);
                        break;

                    case "copyToClipboard":
                        Clipboard.SetText(msg.Data);
                        SendToJS("copyResult", new { success = true });
                        break;

                    case "deleteAllAccounts":
                        Accounts.Clear();
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                        break;
                        
                    case "deleteAccount":
                        DeleteAccount(msg.Data);
                        break;
                        
                    case "updateBalance":
                        UpdateBalance(msg.Data);
                        break;
                        
                    case "massUpdate":
                        MassUpdateAccounts(msg.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", ex.Message);
            }
        }

        private void MassUpdateAccounts(string data)
        {
            try
            {
                var updateData = JsonSerializer.Deserialize<MassUpdateData>(data);
                if (updateData?.AccountIds != null && updateData.Fields != null)
                {
                    foreach (var accountId in updateData.AccountIds)
                    {
                        var account = GetAccountById(accountId);
                        if (account != null)
                        {
                            foreach (var field in updateData.Fields)
                            {
                                switch (field.Key)
                                {
                                    case "Proxy": account.Proxy = field.Value; break;
                                    case "Notes": account.Notes = field.Value; break;
                                    case "Status": account.Status = field.Value; break;
                                }
                            }
                        }
                    }
                    SaveAccounts();
                    SendToJS("accounts", Accounts);
                    SendToJS("massUpdateComplete", new { count = updateData.AccountIds.Length });
                }
            }
            catch (Exception ex)
            {
                SendToJS("error", ex.Message);
            }
        }

        private async Task GetInventory(string parameters)
        {
            try
            {
                var parts = parameters.Split('|');
                string steamId = parts[0];
                string appId = parts.Length > 1 ? parts[1] : "730";
                
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                string url = $"https://steamcommunity.com/inventory/{steamId}/{appId}/2?l=russian&count=200";
                string response = await client.GetStringAsync(url);
                
                var inventory = JsonSerializer.Deserialize<SteamInventory>(response);
                SendToJS("inventoryData", new { appId, data = inventory });
            }
            catch
            {
                SendToJS("inventoryError", "Не удалось загрузить инвентарь.");
            }
        }

        private void RunASF(string login)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string asfPath = Path.Combine(exeFolder, "ASF.exe");
                
                if (File.Exists(asfPath))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = asfPath,
                            Arguments = $"--command --cryptkey \"{login}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    SendToJS("asfStarted", $"ASF запущен для {login}");
                    
                    var account = GetAccountByLogin(login);
                    if (account != null)
                    {
                        account.Status = "Online";
                        account.LastLogin = DateTime.Now.ToString("o");
                        SaveAccounts();
                        SendToJS("accounts", Accounts);
                    }
                }
                else
                {
                    SendToJS("asfError", $"ASF.exe не найден в папке с программой");
                }
            }
            catch (Exception ex)
            {
                SendToJS("asfError", ex.Message);
            }
        }

        private void RunASFForAll()
        {
            int successCount = 0;
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string exeFolder = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string asfPath = Path.Combine(exeFolder, "ASF.exe");
            
            if (!File.Exists(asfPath))
            {
                SendToJS("asfError", $"ASF.exe не найден");
                return;
            }
            
            foreach (var account in Accounts)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = asfPath,
                            Arguments = $"--command --cryptkey \"{account.Login}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    account.Status = "Online";
                    account.LastLogin = DateTime.Now.ToString("o");
                    successCount++;
                }
                catch { }
            }
            SaveAccounts();
            SendToJS("accounts", Accounts);
            SendToJS("asfStarted", $"ASF запущен для {successCount} аккаунтов");
        }
        
        private void DeleteAccount(string accountId)
        {
            var account = GetAccountById(accountId);
            if (account != null)
            {
                Accounts.Remove(account);
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }
        
        private void UpdateBalance(string data)
        {
            var parts = data.Split('|');
            string accountId = parts[0];
            string newBalance = parts[1];
            
            var account = GetAccountById(accountId);
            if (account != null)
            {
                account.Balance = newBalance;
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }

        private Account? GetAccountByLogin(string login)
        {
            foreach (var acc in Accounts)
                if (acc.Login == login) return acc;
            return null;
        }

        private void UpdateLastLogin(string accountId)
        {
            var account = GetAccountById(accountId);
            if (account != null)
            {
                account.LastLogin = DateTime.Now.ToString("o");
                SaveAccounts();
                SendToJS("accounts", Accounts);
            }
        }

        private Account? GetAccountById(string id)
        {
            foreach (var acc in Accounts)
                if (acc.Id == id) return acc;
            return null;
        }

        private void SendToJS(string type, object data)
        {
            if (webView?.CoreWebView2 == null) return;
            
            try
            {
                string json = JsonSerializer.Serialize(new { type, data });
                webView.CoreWebView2.ExecuteScriptAsync($"window.receiveFromCSharp({json});");
            }
            catch { }
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    string json = File.ReadAllText(dataPath);
                    var loaded = JsonSerializer.Deserialize<ObservableCollection<Account>>(json);
                    if (loaded != null)
                    {
                        Accounts.Clear();
                        foreach (var acc in loaded)
                            Accounts.Add(acc);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void SaveAccounts()
        {
            try
            {
                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);
                
                string json = JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveAccounts();
        }
    }

    public class Account : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Login { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmailPass { get; set; } = "";
        public string Proxy { get; set; } = "";
        public string Pin { get; set; } = "";
        public string MaFile { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "Offline";
        public string Balance { get; set; } = "0 ₽";
        public string SteamId { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string LastLogin { get; set; } = "";
        public int CardsRemaining { get; set; } = 0;
        public int GamesCount { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WebMessage
    {
        public string Action { get; set; } = "";
        public string Data { get; set; } = "";
    }

    public class MassUpdateData
    {
        public string[] AccountIds { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    public class SteamInventory
    {
        public bool success { get; set; }
        public SteamInventoryItem[]? assets { get; set; }
        public SteamInventoryDescription[]? descriptions { get; set; }
        public int total_inventory_count { get; set; }
    }

    public class SteamInventoryItem
    {
        public string assetid { get; set; } = "";
        public string classid { get; set; } = "";
        public int amount { get; set; }
    }

    public class SteamInventoryDescription
    {
        public string classid { get; set; } = "";
        public string name { get; set; } = "";
        public string market_hash_name { get; set; } = "";
        public string icon_url { get; set; } = "";
        public string type { get; set; } = "";
        public string rarity { get; set; } = "";
    }
}
