<script setup lang="ts">
import { computed, ref, watchEffect } from 'vue';
import type { HttpImageBodyEntry, HttpBodyEntry } from '@/types/http';

const props = defineProps<{
  contentType: string | null,
  body: HttpImageBodyEntry;
  loadBodyFn: (id: string) => Promise<HttpBodyEntry>;
}>();

const localBinaryContent = ref<string | null>(null);
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const handleLoad = async (id: string) => {
  isLoading.value = true;
  errorMessage.value = null;  
  try {
    const data = await props.loadBodyFn(id) as HttpImageBodyEntry;
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
  const mime = contentType.value || 'image/png';
  return `data:${mime};base64,${localBinaryContent.value}`
});

const base64ToBlob = (
  base64: string,
  mime: string,
): Blob => {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);

  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  return new Blob([bytes], { type: mime });
};

const convertImageToPng = async (
  blob: Blob,
): Promise<Blob> => {
  const bitmap = await createImageBitmap(blob);

  const canvas = document.createElement("canvas");

  canvas.width = bitmap.width;
  canvas.height = bitmap.height;

  const ctx = canvas.getContext("2d");

  if (!ctx) {
    throw new Error("Unable to create canvas context");
  }

  ctx.drawImage(bitmap, 0, 0);

  bitmap.close();

  return new Promise((resolve, reject) => {
    canvas.toBlob(
      pngBlob => {
        if (pngBlob) {
          resolve(pngBlob);
        } else {
          reject(
            new Error("Unable to convert image to PNG")
          );
        }
      },
      "image/png",
    );
  });
};

const copyImageToClipboard = async (
  base64: string,
  mime: string,
): Promise<void> => {
  const imageBlob = base64ToBlob(base64, mime);

  const pngBlob =
    mime === "image/png"
      ? imageBlob
      : await convertImageToPng(imageBlob);

  await navigator.clipboard.write([
    new ClipboardItem({
      "image/png": pngBlob,
    }),
  ]);
};

const copyToClipboard = async () => {
  if (!localBinaryContent.value) return;
  try {
    const mime = contentType.value || 'image/png';
    await copyImageToClipboard(localBinaryContent.value, mime);
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
  <div v-if="errorMessage" class="error-alert">
    {{ errorMessage }}
  </div>
  <div v-else class="image-viewer-container">
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