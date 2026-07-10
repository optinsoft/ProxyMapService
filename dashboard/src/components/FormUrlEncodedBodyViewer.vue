<script setup lang="ts">
import { computed } from 'vue';
import type { HttpFormUrlEncodedBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpFormUrlEncodedBodyEntry;
}>();

const entries = computed(() => {
  const params = new URLSearchParams(props.body.content);

  return [...params.entries()].map(([key, value]) => ({
    key,
    value,
  }));
});

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(props.body.content);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadAsFile = () => {
  const blob = new Blob([props.body.content], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.txt`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div class="form-viewer-container">
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
</style>