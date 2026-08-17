<script setup lang="ts">
import { computed, ref, watchEffect } from 'vue';
import type { HttpHtmlBodyEntry, HttpBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpHtmlBodyEntry;
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();

const tab = ref<'source' | 'preview'>('source');
const localContent = ref<string | null>(null);
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const handleLoad = async (id: string) => {
  isLoading.value = true;
  errorMessage.value = null;  
  try {
    const data = await props.loadBodyFn(id) as HttpHtmlBodyEntry;
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

const iframeSrcDoc = computed(() => localContent.value || '');

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

  const blob = new Blob([localContent.value], { type: 'text/html' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.html`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div v-if="errorMessage" class="error-alert">
    {{ errorMessage }}
  </div>
  <div v-else class="html-viewer-container">
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
      >{{ localContent }}</pre>
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