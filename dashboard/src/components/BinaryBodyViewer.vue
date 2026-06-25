<script setup lang="ts">
import type { HttpBinaryBodyEntry } from '@/types/log';

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
    <div>
      Size: {{ body.length.toLocaleString() }} byte(s)
    </div>

    <div>
      Type: {{ body.contentType ?? 'unknown' }}
    </div>

    <button @click="download">
      Download
    </button>
  </div>
</template>