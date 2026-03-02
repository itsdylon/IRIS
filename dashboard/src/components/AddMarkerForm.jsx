import { useState } from 'react';

export default function AddMarkerForm({ createMarker }) {
  const [name, setName] = useState('');
  const [lat, setLat] = useState('');
  const [lng, setLng] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!name || !lat || !lng) {
      alert('Please fill out all fields.');
      return;
    }
    createMarker({
      label: name,
      lat: parseFloat(lat),
      lng: parseFloat(lng),
      type: 'manual',
    });
    setName('');
    setLat('');
    setLng('');
  };

  return (
    <form onSubmit={handleSubmit} className="add-marker-form">
      <h3>Add New Marker</h3>
      <div className="form-group">
        <label htmlFor="marker-name">Name</label>
        <input
          type="text"
          id="marker-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Klaus Advanced Computing Building"
        />
      </div>
      <div className="form-group">
        <label htmlFor="marker-lat">Latitude</label>
        <input
          type="number"
          id="marker-lat"
          value={lat}
          onChange={(e) => setLat(e.target.value)}
          placeholder="e.g. 33.7773"
        />
      </div>
      <div className="form-group">
        <label htmlFor="marker-lng">Longitude</label>
        <input
          type="number"
          id="marker-lng"
          value={lng}
          onChange={(e) => setLng(e.target.value)}
          placeholder="e.g. -84.3963"
        />
      </div>
      <button type="submit">Add Marker</button>
    </form>
  );
}
