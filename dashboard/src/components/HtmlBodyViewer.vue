<script setup lang="ts">
import { computed, ref } from 'vue';
import type { HttpHtmlBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpHtmlBodyEntry;
}>();

const tab = ref<'source' | 'preview'>('source');

const iframeSrcDoc = computed(() => props.body.content);

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(props.body.content);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadAsFile = () => {
  const blob = new Blob([props.body.content], { type: 'text/html' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.html`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div class="html-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <button @click="downloadAsFile" class="action-btn">
        💾 Download .html
      </button>
    </div>
    <div class="html-viewer">
      <div class="tabs">
        <button
          :class="{ active: tab === 'source' }"
          @click="tab = 'source'"
        >
          Source
        </button>
        
        <button
          :class="{ active: tab === 'preview' }"
          @click="tab = 'preview'"
        >
          Preview
        </button>
      </div>

      <iframe
        v-if="tab === 'preview'"
        class="preview"
        :srcdoc="iframeSrcDoc"
        sandbox="allow-same-origin"
      />

      <pre
        v-else
        class="source"
      >{{ body.content }}</pre>
    </div>
  </div>
</template>

<style scoped>
.html-viewer-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.actions-panel {
  display: flex;
  gap: 8px;
  padding-bottom: 8px;
}

.action-btn {
  background: #3c3c3c; color: #fff; border: 1px solid #555;
  padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 13px;

}

.action-btn:hover {
  background-color: #444;
}

.html-viewer {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.tabs {
  display: flex;
  margin-top: 8px;
  margin-bottom: 12px;
  gap: 4px;
}

.tabs button {
  flex: 1;
  border: 1px solid #3c3c3c;
  background: #252526;
  color: #ccc;
  padding: 8px;
  cursor: pointer;
  max-width: 200px;
}

.tabs button.active {
  background: #094771;
  color: white;
}

.preview {
  flex: 1;
  border: none;
  min-height: 600px;
  background-color: #ffffff;
}

.source {
  flex: 1;
  overflow: auto;
  margin: 0;
  padding: 12px;
  white-space: pre-wrap;
  font-family: monospace;
}
</style>