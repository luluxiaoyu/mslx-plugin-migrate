<template>
  <div
    class="design-card relative flex flex-col bg-[var(--td-bg-color-container)]/80 rounded-2xl border border-[var(--td-component-border)] shadow-sm transition-all duration-300"
  >
    <t-loading :loading="loading" show-overlay>
      <div class="p-5 sm:p-6 sm:px-8">
        <!-- 头部栏 -->
        <div
          class="flex items-center justify-between mb-6 pb-4 border-b border-dashed border-zinc-200/70 dark:border-zinc-700/60"
        >
          <div class="flex items-center gap-3">
            <div
              class="w-1.5 h-5 bg-[var(--color-primary)] rounded-full shadow-[0_0_8px_var(--color-primary-light)] opacity-90"
            ></div>
            <h2 class="text-lg font-bold text-[var(--td-text-color-primary)] m-0 leading-none tracking-tight">
              整机文件迁移
            </h2>
          </div>

          <div class="flex items-center gap-2">
            <t-button variant="outline" theme="default" size="small" :loading="loading || backupsLoading" @click="refreshAll">
              <template #icon><refresh-icon /></template>
              刷新
            </t-button>
            <t-button theme="primary" variant="outline" size="small" @click="openExportDialog">
              <template #icon><download-icon /></template>
              导出迁移包
            </t-button>
            <t-button theme="primary" size="small" @click="openImportDialog">
              <template #icon><upload-icon /></template>
              导入迁移包
            </t-button>
          </div>
        </div>

        <!-- 卡片主体内容 -->
        <div class="text-sm text-[var(--td-text-color-secondary)] leading-relaxed">
          将本机的服务端实例、FRP 穿透隧道、系统设置及用户数据打包导出为迁移文件，或在当前面板中快速还原。
        </div>

        <!-- 警告提示（严格无Emoji） -->
        <t-alert theme="warning" :close="false" class="mt-4">
          建议关闭正在运行的服务端后再进行操作，以保证数据完整性。
        </t-alert>

        <!-- 统计信息 -->
        <div class="flex items-center gap-3 mt-5 mb-3">
          <span class="text-xs font-extrabold text-[var(--td-text-color-secondary)] uppercase tracking-widest">
            数据统计
          </span>
          <div class="h-px bg-zinc-200/60 dark:bg-zinc-700/60 flex-1"></div>
        </div>

        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div class="flex items-center gap-2.5 p-3 rounded-xl bg-zinc-50 dark:bg-zinc-800/40 border border-zinc-200/60 dark:border-zinc-700/60">
            <server-icon class="text-base text-[var(--color-primary)] opacity-80" />
            <div class="flex flex-col">
              <span class="text-xs text-[var(--td-text-color-placeholder)]">服务端实例</span>
              <span class="text-sm font-bold text-[var(--td-text-color-primary)]">{{ summary.servers.length }} 个</span>
            </div>
          </div>

          <div class="flex items-center gap-2.5 p-3 rounded-xl bg-zinc-50 dark:bg-zinc-800/40 border border-zinc-200/60 dark:border-zinc-700/60">
            <internet-icon class="text-base text-[var(--color-primary)] opacity-80" />
            <div class="flex flex-col">
              <span class="text-xs text-[var(--td-text-color-placeholder)]">FRP 隧道</span>
              <span class="text-sm font-bold text-[var(--td-text-color-primary)]">{{ summary.frpTunnels.length }} 条</span>
            </div>
          </div>

          <div class="flex items-center gap-2.5 p-3 rounded-xl bg-zinc-50 dark:bg-zinc-800/40 border border-zinc-200/60 dark:border-zinc-700/60">
            <user-icon class="text-base text-[var(--color-primary)] opacity-80" />
            <div class="flex flex-col">
              <span class="text-xs text-[var(--td-text-color-placeholder)]">用户账号</span>
              <span class="text-sm font-bold text-[var(--td-text-color-primary)]">{{ summary.userCount }} 个</span>
            </div>
          </div>

          <div class="flex items-center gap-2.5 p-3 rounded-xl bg-zinc-50 dark:bg-zinc-800/40 border border-zinc-200/60 dark:border-zinc-700/60">
            <app-icon class="text-base text-[var(--color-primary)] opacity-80" />
            <div class="flex flex-col">
              <span class="text-xs text-[var(--td-text-color-placeholder)]">已装插件</span>
              <span class="text-sm font-bold text-[var(--td-text-color-primary)]">{{ summary.pluginCount }} 个</span>
            </div>
          </div>
        </div>

        <!-- 历史备份文件管理列表 -->
        <div class="flex items-center justify-between gap-3 mt-6 mb-3">
          <div class="flex items-center gap-2">
            <span class="text-xs font-extrabold text-[var(--td-text-color-secondary)] uppercase tracking-widest">
              历史迁移包
            </span>
            <span class="text-xs font-mono text-[var(--td-text-color-placeholder)]">({{ backups.length }})</span>
          </div>
          <div class="flex items-center gap-2">
            <t-button variant="text" size="small" theme="primary" :loading="backupsLoading" @click="fetchBackups">
              <template #icon><refresh-icon /></template>
              刷新列表
            </t-button>
          </div>
        </div>

        <div v-if="backups.length === 0" class="p-6 text-center rounded-xl bg-zinc-50 dark:bg-zinc-800/30 border border-dashed border-zinc-200 dark:border-zinc-700/60 flex flex-col items-center justify-center gap-2">
          <span class="text-xs text-[var(--td-text-color-placeholder)]">
            暂无已生成的迁移包
          </span>
          <t-button size="small" variant="outline" theme="default" :loading="backupsLoading" @click="fetchBackups">
            <template #icon><refresh-icon /></template>
            刷新列表
          </t-button>
        </div>

        <div v-else class="flex flex-col gap-2">
          <div
            v-for="item in backups"
            :key="item.fileName"
            class="flex items-center justify-between p-3 rounded-xl bg-zinc-50 dark:bg-zinc-800/40 border border-zinc-200/60 dark:border-zinc-700/60 transition-colors hover:border-[var(--color-primary)]/40"
          >
            <div class="flex items-center gap-3 min-w-0 pr-3">
              <file-icon class="text-lg text-[var(--color-primary)] flex-shrink-0" />
              <div class="flex flex-col min-w-0">
                <span class="text-xs font-mono font-bold text-[var(--td-text-color-primary)] truncate" :title="item.fileName">
                  {{ item.fileName }}
                </span>
                <span class="text-[11px] text-[var(--td-text-color-placeholder)] font-mono">
                  大小: {{ item.fileSizeText }} · 创建时间: {{ formatTime(item.createdAt) }}
                </span>
              </div>
            </div>

            <div class="flex items-center gap-1.5 flex-shrink-0">
              <t-button size="small" theme="primary" variant="text" @click="downloadFile(item.fileName)">
                <template #icon><download-icon /></template>
                下载
              </t-button>
              <t-popconfirm content="确定要删除该迁移包文件吗？" @confirm="deleteFile(item.fileName)">
                <t-button size="small" theme="danger" variant="text">
                  <template #icon><delete-icon /></template>
                  删除
                </t-button>
              </t-popconfirm>
            </div>
          </div>
        </div>
      </div>
    </t-loading>

    <!-- 导出弹窗 -->
    <t-dialog
      v-model:visible="exportVisible"
      header="导出整机迁移包"
      width="620px"
      :confirm-btn="exportLoading ? '正在导出...' : '开始导出'"
      :confirm-btn-props="{ loading: exportLoading }"
      @confirm="submitExport"
    >
      <div class="py-2 flex flex-col gap-4">
        <t-alert theme="warning" :close="false">
          建议关闭正在运行的服务端后再进行操作，以保证数据完整性。
        </t-alert>

        <div class="text-xs text-[var(--td-text-color-secondary)]">
          选择需要导出的数据项，导出任务将在后台异步执行。
        </div>

        <div class="flex flex-col gap-3 p-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl border border-zinc-200 dark:border-zinc-700/60">
          <t-checkbox v-model="exportOptions.ExportServers">
            <span class="font-bold text-[var(--td-text-color-primary)]">服务端实例</span>
          </t-checkbox>
          <div
            v-if="exportOptions.ExportServers && summary.servers.length > 0"
            class="ml-6 pl-3 border-l-2 border-zinc-200 dark:border-zinc-700 flex flex-col gap-1.5 max-h-40 overflow-y-auto"
          >
            <div class="flex items-center justify-between text-xs text-[var(--td-text-color-placeholder)] mb-1">
              <span>选择要导出的实例：</span>
              <a class="cursor-pointer text-[var(--color-primary)]" @click="toggleSelectAllServers">
                {{ exportOptions.SelectedServerIds.length === summary.servers.length ? '取消全选' : '全选' }}
              </a>
            </div>
            <t-checkbox-group v-model="exportOptions.SelectedServerIds">
              <div v-for="srv in summary.servers" :key="srv.id" class="text-xs py-0.5">
                <t-checkbox :value="srv.id">{{ srv.name }}</t-checkbox>
              </div>
            </t-checkbox-group>
          </div>

          <t-checkbox v-model="exportOptions.ExportFrp">
            <span class="font-bold text-[var(--td-text-color-primary)]">FRP 隧道配置</span>
          </t-checkbox>

          <t-checkbox v-model="exportOptions.ExportUsers">
            <span class="font-bold text-[var(--td-text-color-primary)]">用户数据</span>
          </t-checkbox>

          <t-checkbox v-model="exportOptions.ExportSystemSettings">
            <span class="font-bold text-[var(--td-text-color-primary)]">系统设置</span>
          </t-checkbox>

          <t-checkbox v-model="exportOptions.ExportPlugins">
            <span class="font-bold text-[var(--td-text-color-primary)]">插件数据</span>
          </t-checkbox>
        </div>
      </div>
    </t-dialog>

    <!-- 导入弹窗 -->
    <t-dialog
      v-model:visible="importVisible"
      header="导入整机迁移包"
      width="680px"
      :confirm-btn="importLoading ? '正在处理...' : '开始导入'"
      :confirm-btn-props="{ loading: importLoading, disabled: !canSubmitImport }"
      @confirm="submitImport"
    >
      <div class="py-2 flex flex-col gap-4">
        <t-alert theme="info" :close="false">
          导入采用追加模式，不会覆盖当前已有实例、隧道及同名用户。
        </t-alert>

        <!-- 导入模式切换 -->
        <t-radio-group v-model="importMode" variant="default-filled">
          <t-radio-button value="localFolder">
            <template #icon><folder-open-icon /></template>
            本地目录导入
          </t-radio-button>
          <t-radio-button value="hostUpload" :disabled="!hasHostUpload">
            <template #icon><upload-icon /></template>
            上传文件导入
          </t-radio-button>
        </t-radio-group>

        <!-- 方式 1: 识别插件数据 Imports 目录下的 Zip -->
        <div v-if="importMode === 'localFolder'" class="flex flex-col gap-3">
          <div class="p-3.5 bg-zinc-50 dark:bg-zinc-800/40 rounded-xl border border-zinc-200 dark:border-zinc-700/60 text-xs text-[var(--td-text-color-secondary)]">
            <div class="font-bold text-[var(--td-text-color-primary)] mb-1 flex items-center gap-1.5">
              <file-icon class="text-[var(--color-primary)]" />
              文件存放目录
            </div>
            请将需要导入的 Zip 文件上传或放置到服务器以下路径：
            <div class="mt-1.5 p-2 bg-white dark:bg-zinc-900 rounded border border-zinc-200 dark:border-zinc-700 font-mono text-[11px] text-[var(--color-primary)] break-all select-all">
              {{ summary.importsPath || '正在获取路径...' }}
            </div>
          </div>

          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-xs font-bold text-[var(--td-text-color-secondary)]">已有导入包：</span>
              <span class="text-xs font-mono text-[var(--td-text-color-placeholder)]">({{ importFiles.length }})</span>
            </div>
            <t-button variant="text" size="small" theme="primary" :loading="importFilesLoading" @click="fetchImportFiles">
              <template #icon><refresh-icon /></template>
              刷新列表
            </t-button>
          </div>

          <div v-if="importFiles.length === 0" class="p-5 text-center rounded-xl bg-zinc-50 dark:bg-zinc-800/30 border border-dashed border-zinc-200 dark:border-zinc-700/60 flex flex-col items-center justify-center gap-2 text-xs text-[var(--td-text-color-placeholder)]">
            <span>{{ importFilesLoading ? '正在读取文件列表...' : '未检测到可导入的 Zip 文件，请将文件放入指定目录后刷新。' }}</span>
            <t-button v-if="!importFilesLoading" size="small" variant="outline" theme="default" @click="fetchImportFiles">
              <template #icon><refresh-icon /></template>
              刷新列表
            </t-button>
          </div>

          <t-radio-group v-else v-model="selectedLocalFile" class="flex flex-col gap-2 max-h-48 overflow-y-auto pr-1">
            <div
              v-for="file in importFiles"
              :key="file.fileName"
              class="flex items-center justify-between p-3 rounded-xl border transition-all cursor-pointer"
              :class="selectedLocalFile === file.fileName ? 'border-[var(--color-primary)] bg-[var(--color-primary-light)]/10' : 'border-zinc-200/80 dark:border-zinc-700/60 bg-zinc-50/60 dark:bg-zinc-800/30'"
              @click="selectedLocalFile = file.fileName"
            >
              <div class="flex items-center gap-2.5 min-w-0">
                <t-radio :value="file.fileName" />
                <div class="flex flex-col min-w-0">
                  <span class="font-mono text-xs font-bold text-[var(--td-text-color-primary)] truncate">{{ file.fileName }}</span>
                  <span class="text-[11px] text-[var(--td-text-color-placeholder)] font-mono">{{ file.fileSizeText }} · {{ formatTime(file.createdAt) }}</span>
                </div>
              </div>
            </div>
          </t-radio-group>
        </div>

        <!-- 方式 2: 调用宿主上传组件 -->
        <div v-else-if="importMode === 'hostUpload'" class="flex flex-col gap-3">
          <div
            class="relative p-6 border-2 border-dashed rounded-xl flex flex-col items-center justify-center gap-3 transition-all duration-200 cursor-pointer select-none"
            :class="isDragOver
              ? 'border-[var(--color-primary)] bg-[var(--color-primary-light)]/15 scale-[1.01]'
              : 'border-zinc-300 dark:border-zinc-700 bg-zinc-50/50 dark:bg-zinc-800/20 hover:border-[var(--color-primary)]/60'"
            @dragenter.prevent="onDragEnter"
            @dragover.prevent="onDragOver"
            @dragleave.prevent="onDragLeave"
            @drop.prevent="onFileDrop"
            @click="triggerFileInput"
          >
            <input ref="fileInput" type="file" accept=".zip" class="hidden" @change="handleFileChange" />
            <div
              class="pointer-events-none w-12 h-12 rounded-full flex items-center justify-center transition-colors"
              :class="isDragOver ? 'bg-[var(--color-primary)] text-white' : 'bg-zinc-200/60 dark:bg-zinc-700/60 text-zinc-500 dark:text-zinc-400'"
            >
              <upload-icon class="text-2xl" />
            </div>
            <div class="pointer-events-none flex flex-col items-center gap-1 text-center">
              <div class="text-xs font-bold text-[var(--td-text-color-primary)]">
                {{ isDragOver ? '释放鼠标以上传文件' : (selectedUploadFile ? '点击或拖拽文件可更换' : '点击选择或拖拽迁移包至此处') }}
              </div>
              <div class="text-[11px] text-[var(--td-text-color-placeholder)]">
                仅支持 .zip 格式的整机迁移包
              </div>
            </div>
            <div
              v-if="selectedUploadFile"
              class="mt-1 px-3 py-1.5 rounded-lg bg-white dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 flex items-center gap-2 text-xs font-mono text-[var(--td-text-color-primary)] shadow-sm"
              @click.stop
            >
              <file-icon class="text-base text-[var(--color-primary)]" />
              <span class="truncate max-w-[280px]" :title="selectedUploadFile.name">{{ selectedUploadFile.name }}</span>
              <span class="text-[11px] text-[var(--td-text-color-placeholder)]">({{ formatSize(selectedUploadFile.size) }})</span>
              <t-button size="small" variant="text" theme="danger" class="ml-1 !p-0.5" @click="selectedUploadFile = null">
                <template #icon><delete-icon /></template>
              </t-button>
            </div>
          </div>

          <!-- 宿主上传动态进度条 -->
          <div v-if="importLoading" class="flex flex-col gap-2 p-3 bg-zinc-50 dark:bg-zinc-800/60 rounded-xl border border-zinc-200 dark:border-zinc-700/60">
            <div class="flex items-center justify-between text-xs text-[var(--td-text-color-secondary)]">
              <span>{{ uploadStatusText }}</span>
              <span class="font-mono font-bold text-[var(--color-primary)]">{{ uploadPercent }}%</span>
            </div>
            <t-progress :percentage="uploadPercent" theme="primary" :stroke-width="6" />
          </div>
        </div>

        <div class="flex flex-col gap-2.5 p-4 bg-zinc-50 dark:bg-zinc-800/50 rounded-xl border border-zinc-200 dark:border-zinc-700/60 text-xs">
          <span class="font-bold text-[var(--td-text-color-primary)]">导入项目：</span>
          <t-checkbox v-model="importOptions.ImportServers">
            服务端实例
          </t-checkbox>
          <t-checkbox v-model="importOptions.ImportFrp">
            FRP 隧道配置
          </t-checkbox>
          <t-checkbox v-model="importOptions.ImportUsers">
            用户数据
          </t-checkbox>
          <t-checkbox v-model="importOptions.ImportSystemSettings">
            系统通用设置
          </t-checkbox>
          <t-checkbox v-model="importOptions.ImportPlugins">
            插件数据
          </t-checkbox>
        </div>
      </div>
    </t-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch, computed } from 'vue';
import { MessagePlugin } from 'tdesign-vue-next';
import {
  DownloadIcon,
  UploadIcon,
  FolderOpenIcon,
  ServerIcon,
  InternetIcon,
  UserIcon,
  AppIcon,
  RefreshIcon,
  DeleteIcon,
  FileIcon
} from 'tdesign-icons-vue-next';
import request from 'mslx-request';

interface BackupItem {
  fileName: string;
  fileSize: number;
  fileSizeText: string;
  createdAt: string;
}

const loading = ref(false);
const backupsLoading = ref(false);

const summary = reactive({
  servers: [] as Array<{ id: number; name: string; core: string; base: string }>,
  frpTunnels: [] as Array<{ id: number; name: string; service: string }>,
  userCount: 0,
  pluginCount: 0,
  importsPath: ''
});

const backups = ref<BackupItem[]>([]);

// 获取当前用户的认证 Token
const getToken = () => {
  return localStorage.getItem('mslx-web-token') || localStorage.getItem('token') || '';
};

// 拉取资产统计
const fetchSummary = async () => {
  if (!request) return;
  try {
    loading.value = true;
    const res: any = await request.get({
      url: '/api/plugin/mslx-plugin-migrate/migration/items'
    });
    const data = res?.data ?? res;
    if (data) {
      summary.servers = data.servers || data.Servers || [];
      summary.frpTunnels = data.frpTunnels || data.FrpTunnels || [];
      summary.userCount = data.userCount ?? data.UserCount ?? 0;
      summary.pluginCount = data.pluginCount ?? data.PluginCount ?? 0;
      summary.importsPath = data.importsPath || data.ImportsPath || '';
    }
  } catch (e: any) {
    console.warn('[MSLX Migration] 拉取概览失败', e);
  } finally {
    loading.value = false;
  }
};

// 拉取已完成的历史备份列表
const fetchBackups = async () => {
  if (!request) return;
  try {
    backupsLoading.value = true;
    const res: any = await request.get({
      url: '/api/plugin/mslx-plugin-migrate/migration/backups'
    });
    const data = res?.data ?? res;
    backups.value = Array.isArray(data) ? data : [];
  } catch (e: any) {
    console.warn('[MSLX Migration] 拉取备份列表失败', e);
  } finally {
    backupsLoading.value = false;
  }
};

const refreshAll = async () => {
  await Promise.all([fetchSummary(), fetchBackups()]);
};

onMounted(() => {
  fetchSummary();
  fetchBackups();
});

// 导出相关
const exportVisible = ref(false);
const exportLoading = ref(false);
const exportOptions = reactive({
  ExportServers: true,
  SelectedServerIds: [] as number[],
  ExportFrp: true,
  SelectedFrpIds: [] as number[],
  ExportUsers: true,
  ExportSystemSettings: true,
  ExportPlugins: true
});

const openExportDialog = () => {
  exportVisible.value = true;
  if (summary.servers.length > 0 && exportOptions.SelectedServerIds.length === 0) {
    exportOptions.SelectedServerIds = summary.servers.map(s => s.id);
  }
};

const toggleSelectAllServers = () => {
  if (exportOptions.SelectedServerIds.length === summary.servers.length) {
    exportOptions.SelectedServerIds = [];
  } else {
    exportOptions.SelectedServerIds = summary.servers.map(s => s.id);
  }
};

// 触发宿主后台任务 Pinia store 刷新并启动轮询
const triggerHostTaskRefresh = () => {
  try {
    const stores = (window as any).MSLX_Stores;
    if (stores && typeof stores.useTaskStore === 'function') {
      const taskStore = stores.useTaskStore();
      if (typeof taskStore.fetchTasks === 'function') {
        taskStore.fetchTasks();
      }
      if (typeof taskStore.startPolling === 'function') {
        taskStore.startPolling();
      }
    }
  } catch (err) {
    console.warn('[MSLX Migration] 刷新宿主任务状态失败:', err);
  }
};

const submitExport = async () => {
  if (!request) return;
  try {
    exportLoading.value = true;
    await request.post({
      url: '/api/plugin/mslx-plugin-migrate/migration/export',
      data: exportOptions
    });
    exportVisible.value = false;
    MessagePlugin.success('导出任务已创建，可在任务中心查看进度');

    // 立即通知宿主任务中心刷新并启动轮询
    triggerHostTaskRefresh();
    
    // 延迟轮询刷新历史列表
    setTimeout(() => {
      fetchBackups();
    }, 3000);
  } catch (e: any) {
    MessagePlugin.error(e.message || '导出任务创建失败');
  } finally {
    exportLoading.value = false;
  }
};

// 安全下载（Query 中携带用户 token 保证直接访问鉴权）
const downloadFile = (fileName: string) => {
  const token = getToken();
  const downloadUrl = `/api/plugin/mslx-plugin-migrate/migration/download/${encodeURIComponent(fileName)}?x-user-token=${encodeURIComponent(token)}&token=${encodeURIComponent(token)}`;
  window.open(downloadUrl, '_blank');
};

// 删除已生成的备份文件
const deleteFile = async (fileName: string) => {
  if (!request) return;
  try {
    await request.delete({
      url: `/api/plugin/mslx-plugin-migrate/migration/backups/${encodeURIComponent(fileName)}`
    });
    MessagePlugin.success('文件已删除');
    fetchBackups();
  } catch (e: any) {
    MessagePlugin.error(e.message || '删除失败');
  }
};

// 导入相关状态
const hasHostUpload = typeof (window as any).useFileUpload === 'function';
const importMode = ref<'localFolder' | 'hostUpload'>('localFolder');
const importVisible = ref(false);
const importLoading = ref(false);
const importFilesLoading = ref(false);
const importFiles = ref<BackupItem[]>([]);
const selectedLocalFile = ref('');

// 宿主上传相关状态
const fileInput = ref<HTMLInputElement | null>(null);
const selectedUploadFile = ref<File | null>(null);
const uploadStatusText = ref('');
const uploadPercent = ref(0);
const hostUploadHook = hasHostUpload ? (window as any).useFileUpload() : null;

const dragCounter = ref(0);
const isDragOver = computed(() => dragCounter.value > 0);

const importOptions = reactive({
  ImportServers: true,
  ImportFrp: true,
  ImportUsers: false,
  ImportSystemSettings: false,
  ImportPlugins: false
});

// 获取服务端 Imports 目录下的待导入 Zip 文件列表
const fetchImportFiles = async () => {
  if (!request) return;
  try {
    importFilesLoading.value = true;
    const res: any = await request.get({
      url: '/api/plugin/mslx-plugin-migrate/migration/import-files'
    });
    const data = res?.data ?? res;
    importFiles.value = Array.isArray(data) ? data : [];
    if (importFiles.value.length > 0 && !selectedLocalFile.value) {
      selectedLocalFile.value = importFiles.value[0].fileName;
    }
  } catch (e: any) {
    console.warn('[MSLX Migration] 拉取待导入文件列表失败', e);
  } finally {
    importFilesLoading.value = false;
  }
};

const openImportDialog = () => {
  importVisible.value = true;
  selectedUploadFile.value = null;
  selectedLocalFile.value = '';
  dragCounter.value = 0;
  uploadPercent.value = 0;
  uploadStatusText.value = '';
  // 若未检测到宿主上传则默认模式锁定为 localFolder
  if (!hasHostUpload) {
    importMode.value = 'localFolder';
  }
  fetchImportFiles();
};

const canSubmitImport = computed(() => {
  if (importMode.value === 'localFolder') {
    return Boolean(selectedLocalFile.value);
  }
  return Boolean(selectedUploadFile.value);
});

const triggerFileInput = () => {
  if (!importLoading.value) {
    fileInput.value?.click();
  }
};

const handleFileChange = (e: Event) => {
  const files = (e.target as HTMLInputElement).files;
  if (files && files.length > 0) {
    const file = files[0];
    if (!file.name.toLowerCase().endsWith('.zip')) {
      MessagePlugin.warning('仅支持上传 .zip 格式的迁移包文件');
      return;
    }
    selectedUploadFile.value = file;
  }
};

const onDragEnter = (e: DragEvent) => {
  e.preventDefault();
  if (importLoading.value) return;
  dragCounter.value++;
};

const onDragOver = (e: DragEvent) => {
  e.preventDefault();
};

const onDragLeave = (e: DragEvent) => {
  e.preventDefault();
  dragCounter.value = Math.max(0, dragCounter.value - 1);
};

const onFileDrop = (e: DragEvent) => {
  e.preventDefault();
  dragCounter.value = 0;
  if (importLoading.value) return;

  const files = e.dataTransfer?.files;
  if (files && files.length > 0) {
    const file = files[0];
    if (!file.name.toLowerCase().endsWith('.zip')) {
      MessagePlugin.warning('仅支持上传 .zip 格式的迁移包文件');
      return;
    }
    selectedUploadFile.value = file;
  }
};

const submitImport = async () => {
  if (!request) return;

  if (importMode.value === 'localFolder') {
    // 方式 1: 直接指定服务端 Imports 目录下的文件名导入
    if (!selectedLocalFile.value) {
      MessagePlugin.warning('请选择需要导入的文件');
      return;
    }

    try {
      importLoading.value = true;
      await request.post({
        url: '/api/plugin/mslx-plugin-migrate/migration/import-local-file',
        data: {
          fileName: selectedLocalFile.value,
          options: importOptions
        }
      });

      MessagePlugin.success('导入任务已创建，可在任务中心查看进度');
      importVisible.value = false;
      triggerHostTaskRefresh();
      fetchSummary();
    } catch (e: any) {
      MessagePlugin.error(e.message || '导入任务创建失败');
    } finally {
      importLoading.value = false;
    }
  } else {
    // 方式 2: 调用宿主上传组件上传文件后通过 uploadId 导入
    if (!hasHostUpload || !hostUploadHook) {
      MessagePlugin.error('未检测到宿主上传组件，请使用本地目录导入');
      return;
    }
    if (!selectedUploadFile.value) {
      MessagePlugin.warning('请选择迁移文件');
      return;
    }

    const file = selectedUploadFile.value;

    try {
      importLoading.value = true;
      uploadPercent.value = 0;
      uploadStatusText.value = '正在准备上传...';

      const stopWatch = watch(
        () => hostUploadHook.uploadProgress.value,
        (val: number) => {
          uploadPercent.value = Math.min(Math.round(val), 98);
          uploadStatusText.value = `正在上传文件: ${uploadPercent.value}%`;
        },
        { immediate: true }
      );

      let uploadId = '';
      try {
        uploadStatusText.value = '正在上传文件...';
        uploadId = await hostUploadHook.startUpload(file);
      } finally {
        stopWatch();
      }

      uploadStatusText.value = '上传完成，正在创建导入任务...';
      uploadPercent.value = 100;

      await request.post({
        url: '/api/plugin/mslx-plugin-migrate/migration/import-by-upload-id',
        data: {
          uploadId,
          options: importOptions
        }
      });

      MessagePlugin.success('导入任务已创建，可在任务中心查看进度');
      importVisible.value = false;
      triggerHostTaskRefresh();
      fetchSummary();
    } catch (e: any) {
      MessagePlugin.error(e.message || '上传或导入失败');
    } finally {
      importLoading.value = false;
    }
  }
};

const formatSize = (bytes: number) => {
  if (!bytes) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const formatTime = (timeStr: string) => {
  if (!timeStr) return '';
  const date = new Date(timeStr);
  if (isNaN(date.getTime())) return timeStr;
  return date.toLocaleString('zh-CN', { hour12: false });
};
</script>

<style scoped>
/* 原生 design-card 样式保证，不污染外部 */
</style>
