<script setup lang="ts">
import type { HttpBinaryBodyEntry } from '@/types/http';

const props = defineProps<{
  body: HttpBinaryBodyEntry;
}>();

function download() {
  const bytes = atob(props.body.binaryContentBase64);

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
  link.download = props.body.id;

  link.click();

  URL.revokeObjectURL(url);
}
</script>

<template>
  <div class="binary-viewer">
    <div class="info-row">
      Size: {{ body.length.toLocaleString() }} byte(s)
    </div>

    <div class="info-row">
      Type: {{ body.contentType ?? 'unknown' }}
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
</style>