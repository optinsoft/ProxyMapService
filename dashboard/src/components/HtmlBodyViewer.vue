<script setup lang="ts">
import { computed, ref } from 'vue';
import type { HttpHtmlBodyEntry } from '@/types/log';

const props = defineProps<{
  body: HttpHtmlBodyEntry;
}>();

const tab = ref<'source' | 'preview'>('source');

const iframeSrcDoc = computed(() => props.body.content);
</script>

<template>
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
</template>

<style scoped>
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