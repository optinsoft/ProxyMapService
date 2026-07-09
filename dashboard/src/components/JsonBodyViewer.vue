<script setup lang="ts">
import { computed } from 'vue';
import type { HttpJsonBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpJsonBodyEntry;
}>();

const formattedJson = computed(() => {
  try {
    return JSON.stringify(
      JSON.parse(props.body.content),
      null,
      2,
    );
  } catch {
    return props.body.content;
  }
});

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(props.body.content);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadAsFile = () => {
  const blob = new Blob([props.body.content], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  
  link.href = url;
  link.download = `body-${Date.now()}.json`;
  link.click();
  
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div class="json-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <button @click="downloadAsFile" class="action-btn">
        💾 Download .json
      </button>
    </div>    
    <pre class="json-viewer">{{ formattedJson }}</pre>
  </div>
</template>

<style scoped>
.json-viewer-container {
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

.json-viewer {
  overflow: auto;
  padding: 12px;
  font-family: monospace;
  white-space: pre-wrap;
}
</style>