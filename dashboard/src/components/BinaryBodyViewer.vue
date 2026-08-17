<script setup lang="ts">
import { ref, watchEffect } from 'vue';
import type { HttpBinaryBodyEntry, HttpBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpBinaryBodyEntry;
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();

const localBinaryContent = ref<string | null>(null);
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const handleLoad = async (id: string) => {
  isLoading.value = true;
  errorMessage.value = null;  
  try {
    const data = await props.loadBodyFn(id) as HttpBinaryBodyEntry;
    localBinaryContent.value = data.binaryContentBase64 || ''; 
  } catch (err) {
    console.error(err);
    errorMessage.value = 'Failed to load content';
  } finally {
    isLoading.value = false;
  }
};

watchEffect(() => {
  if (props.body.binaryContentBase64) {
    localBinaryContent.value = props.body.binaryContentBase64;
    errorMessage.value = null;
  } else if (props.body.hasBinaryContent && props.body.id) {
    handleLoad(props.body.id);
  } else {
    localBinaryContent.value = '';
  }
});

function download() {
  if (!localBinaryContent.value) return;

  const bytes = atob(localBinaryContent.value);

  const array = Uint8Array.from(
    bytes,
    c => c.charCodeAt(0),
  );

  const blob = new Blob(
    [array],
    {
      type:
        props.body.contentType ??
        'application/octet-stream',
    },
  );

  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = props.body.id ?? `file_${Date.now()}`;

  link.click();

  URL.revokeObjectURL(url);
}
</script>

<template>
  <div v-if="errorMessage" class="error-alert">
    {{ errorMessage }}
  </div>
  <div v-else class="binary-viewer">
    <div class="info-row">
      Size: {{ props.body.length.toLocaleString() }} byte(s)
    </div>

    <div class="info-row">
      Type: {{ props.body.contentType ?? 'unknown' }}
    </div>

    <div class="action-row">
      <button 
        class="action-btn" 
        @click="download"
      >
        Download
      </button>
    </div>
  </div>
</template>

<style scoped>
.binary-viewer {
  color: #fff;
  font-size: 13px;
  padding: 0px 8px;
}

.info-row {
  padding: 3px 0px;
}

.action-row {
  margin: 8px 0px;
}

.action-btn {
  background: #3c3c3c; color: #fff; border: 1px solid #555;
  padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 13px;

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