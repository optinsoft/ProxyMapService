<script setup lang="ts">
import { computed, ref, watchEffect } from 'vue';
import type { HttpFormUrlEncodedBodyEntry, HttpBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpFormUrlEncodedBodyEntry;
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();

const localContent = ref<string | null>(null);
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const handleLoad = async (id: string) => {
  isLoading.value = true;
  errorMessage.value = null;  
  try {
    const data = await props.loadBodyFn(id) as HttpFormUrlEncodedBodyEntry;
    localContent.value = data.content || ''; 
  } catch (err) {
    console.error(err);
    errorMessage.value = 'Failed to load content';
  } finally {
    isLoading.value = false;
  }
};

watchEffect(() => {
  if (props.body.content) {
    localContent.value = props.body.content;
    errorMessage.value = null;
  } else if (props.body.hasContent && props.body.id) {
    handleLoad(props.body.id);
  } else {
    localContent.value = '';
  }
});

const entries = computed(() => {
  const params = new URLSearchParams(localContent.value || '');

  return [...params.entries()].map(([key, value]) => ({
    key,
    value,
  }));
});

const copyToClipboard = async () => {
  if (!localContent.value) return;
  try {
    await navigator.clipboard.writeText(localContent.value);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadAsFile = () => {
  if (!localContent.value) return;
  
  const blob = new Blob([localContent.value], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.txt`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div v-if="errorMessage" class="error-alert">
    {{ errorMessage }}
  </div>
  <div v-else class="form-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <button @click="downloadAsFile" class="action-btn">
        💾 Download .txt
      </button>
    </div>    
    <table class="form-table">
      <thead>
        <tr>
          <th>Key</th>
          <th>Value</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="entry in entries"
          :key="`${entry.key}:${entry.value}`"
        >
          <td class="key-column">{{ entry.key }}</td>
          <td class="value-column">{{ entry.value }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.form-viewer-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.actions-panel {
  display: flex;
  gap: 8px;
}

.action-btn {
  background: #3c3c3c; color: #fff; border: 1px solid #555;
  padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 13px;

}

.action-btn:hover {
  background-color: #444;
}

.form-table {
  width: 100%;
  border-collapse: collapse;
  font-family: monospace;
}

.form-table th,
.form-table td {
  border: 1px solid #444;
  padding: 6px 8px;
  text-align: left;
  vertical-align: top;
}

.form-table th {
  background: #2d2d2d;
}

.form-table td {
  word-break: break-all;
}

.key-column {
  width: 150px;
  font-weight: bold;
  vertical-align: top;
}

.value-column {
  word-break: break-all;
  white-space: pre-wrap;
  vertical-align: top;
}

.error-alert {
  color: #f44336;
  background-color: rgba(244, 67, 54, 0.1);
  padding: 10px;
  border-radius: 4px;
  margin-bottom: 15px;
  font-size: 14px;
  border: 1px solid rgba(244, 67, 54, 0.2);
}
</style>