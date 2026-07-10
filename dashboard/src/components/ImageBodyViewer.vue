<script setup lang="ts">
import { computed } from 'vue';
import type { HttpImageBodyEntry } from '@/types/http';

const props = defineProps<{
  contentType: string | null,
  body: HttpImageBodyEntry;
}>();

const mimeToExt: Record<string, string> = {
  'image/jpeg': '.jpg',
  'image/png': '.png',
  'image/gif': '.gif',
  'image/svg+xml': '.svg',
  'image/webp': '.webp',
  'image/bmp': '.bmp'
};

const contentType = computed(() => {
  return props.contentType?.split(';')[0]?.trim() || null
});

const imageUrl = computed(() => {
  const mime = contentType || 'image/png';
  return `data:${mime};base64,${props.body.binaryContentBase64}`
});

const copyToClipboard = async () => {
  try {
    const mime = contentType.value || 'image/png';
  
    const bytes = atob(props.body.binaryContentBase64);
  
    const array = Uint8Array.from(
      bytes,
      c => c.charCodeAt(0),
    );

    const blob = new Blob(
      [array],
      {
        type: mime
      },
    );

    await navigator.clipboard.write([
      new ClipboardItem({ [mime]: blob })
    ]);
  } catch (err) {
    console.error('Unable to copy:', err);
  }
};

const downloadFileExt = computed(() => {
  const mime = contentType.value || 'image/png';
  return mimeToExt[mime] || '.png';
});

const downloadFilename = computed(() => {
  const mime = contentType.value || 'image/png';
  const ext = mimeToExt[mime] || '.png';
  return `body-${Date.now()}${ext}`;
});
</script>

<template>
  <div class="image-viewer-container">
    <div class="actions-panel">
      <button @click="copyToClipboard" class="action-btn">
        📋 Copy
      </button>
      <a :href="imageUrl" :download="downloadFilename" class="action-btn link-btn">
        💾 Download {{ downloadFileExt }}
      </a>
    </div>    
    <div class="image-viewer">
      <img
        :src="imageUrl"
        alt="response image"
      />
    </div>
  </div>
</template>

<style scoped>
.image-viewer-container {
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

.link-btn {
  display: inline-flex;
  align-items: center;
}

.image-viewer {
  overflow: auto;
}

.image-viewer img {
  max-width: 100%;
  max-height: 800px;
}
</style>