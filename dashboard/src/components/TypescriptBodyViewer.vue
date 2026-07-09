<script setup lang="ts">
import type { HttpTypescriptBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpTypescriptBodyEntry;
}>();

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(props.body.content);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadAsFile = () => {
  const blob = new Blob([props.body.content], { type: 'application/typescript' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.ts`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div class="text-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <button @click="downloadAsFile" class="action-btn">
        💾 Download .ts
      </button>
    </div>    
    <pre class="text-viewer">{{ body.content }}</pre>
  </div>

</template>

<style scoped>
.text-viewer-container {
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

.text-viewer {
  overflow: auto;
  padding: 12px;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>