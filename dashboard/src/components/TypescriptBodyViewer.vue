<script setup lang="ts">
import { ref, watchEffect } from 'vue';
import type { HttpTypescriptBodyEntry, HttpBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpTypescriptBodyEntry;
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();

const localContent = ref<string | null>(null);
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const handleLoad = async (id: string) => {
  isLoading.value = true;
  errorMessage.value = null;  
  try {
    const data = await props.loadBodyFn(id) as HttpTypescriptBodyEntry;
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

  const blob = new Blob([localContent.value], { type: 'application/typescript' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.ts`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div v-if="errorMessage" class="error-alert">
    {{ errorMessage }}
  </div>
  <div v-else class="text-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <button @click="downloadAsFile" class="action-btn">
        💾 Download .ts
      </button>
    </div>    
    <pre class="text-viewer">{{ localContent }}</pre>
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